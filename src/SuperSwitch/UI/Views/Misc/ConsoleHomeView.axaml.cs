using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.Systems.Configuration;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Ava.UI.Windows;
using SuperSwitch.Common.Logging;
using SuperSwitch.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SuperSwitch.Ava.UI.Views.Misc
{
    /// <summary>
    /// SuperSwitch: console-style home screen. Every game is a large tile; the selected one is started with
    /// Play, Enter, a double-click or the controller's A button.
    /// </summary>
    public partial class ConsoleHomeView : SuperSwitchControl<MainWindowViewModel>
    {
        // Controller navigation: the home screen polls the connected controllers itself, because the emulator
        // only reads them while a game is running. Polling stops whenever this view is not on screen.
        private const float StickThreshold = 0.5f; // Sticks must be pushed at least halfway before they count.
        private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(33);
        private static readonly long _firstRepeatTicks = Stopwatch.Frequency * 400 / 1000;
        private static readonly long _nextRepeatTicks = Stopwatch.Frequency * 140 / 1000;
        private static readonly long _refreshTicks = Stopwatch.Frequency * 2;

        private enum MoveDirection
        {
            None,
            Left,
            Right,
            Up,
            Down,
        }

        private readonly Dictionary<string, IGamepad> _gamepads = new();
        private DispatcherTimer _pollTimer;
        private long _nextGamepadRefresh;
        private MoveDirection _heldDirection;
        private long _nextMoveTime;
        private bool _buttonsHeld = true; // Ignore buttons still held down from the game or dialog just left.

        public ConsoleHomeView() => InitializeComponent();

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            // Arrow keys and Enter work straight away, without clicking a game first.
            GameList.Focus();

            UpdateAutoRecordChip();

            _buttonsHeld = true;
            _heldDirection = MoveDirection.None;
            _nextGamepadRefresh = 0;

            _pollTimer ??= new DispatcherTimer(_pollInterval, DispatcherPriority.Input, (_, _) => PollControllers());
            _pollTimer.Start();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _pollTimer?.Stop();

            ReleaseGamepads();

            base.OnUnloaded(e);
        }

        private void UpdateAutoRecordChip()
        {
            try
            {
                AutoRecordChip.Text = ConfigurationState.Instance.Graphics.EnableAutoRecording.Value
                    ? "Auto-record on"
                    : "Auto-record off";
            }
            catch (Exception)
            {
                // Configuration not available (design time).
            }
        }

        private async void PlaySelected()
        {
            if (DataContext is not MainWindowViewModel viewModel || viewModel.IsGameRunning)
            {
                return;
            }

            ApplicationData application = viewModel.ConsoleSelectedApplication;

            if (application == null)
            {
                return;
            }

            viewModel.SelectedIcon = application.Icon;

            await viewModel.LoadApplication(application);
        }

        private async void OpenSettings()
        {
            if (TopLevel.GetTopLevel(this) is MainWindow window)
            {
                await window.OpenSettingsFromHome();

                // Auto-record may have been changed in Settings.
                UpdateAutoRecordChip();
            }
        }

        private void OpenGameOptions(Control anchor)
        {
            // The same per-game menu as a right-click on a tile (saves, mods, updates, game settings, ...).
            GameList.ContextFlyout?.ShowAt(anchor);
        }

        private void Play_Click(object sender, RoutedEventArgs e) => PlaySelected();

        private void GameList_DoubleTapped(object sender, TappedEventArgs e) => PlaySelected();

        private void GameList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;

                PlaySelected();
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void GameOptions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control anchor)
            {
                OpenGameOptions(anchor);
            }
        }

        // ---- Controller navigation ----

        private void PollControllers()
        {
            try
            {
                PollControllersCore();
            }
            catch (Exception ex)
            {
                // Never let a controller problem take the home screen down; just stop listening to controllers.
                _pollTimer?.Stop();

                ReleaseGamepads();

                Logger.Warning?.Print(LogClass.UI, $"Controller navigation on the home screen was turned off: {ex.Message}");
            }
        }

        private void PollControllersCore()
        {
            if (TopLevel.GetTopLevel(this) is not MainWindow window ||
                DataContext is not MainWindowViewModel viewModel ||
                window.InputManager == null)
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();

            if (now >= _nextGamepadRefresh)
            {
                _nextGamepadRefresh = now + _refreshTicks;

                RefreshGamepads(window.InputManager.GamepadDriver);
            }

            // Only while the home screen itself is in front: not during a game, a dialog or another window.
            if (!window.IsActive || !IsEffectivelyVisible || viewModel.IsGameRunning || !viewModel.IsConsoleHome)
            {
                _buttonsHeld = true;
                _heldDirection = MoveDirection.None;

                return;
            }

            MoveDirection direction = MoveDirection.None;
            bool play = false;
            bool options = false;
            bool settings = false;

            foreach (IGamepad gamepad in _gamepads.Values)
            {
                GamepadStateSnapshot state = gamepad.GetStateSnapshot();
                (float stickX, float stickY) = state.GetStick(StickInputId.Left);
                (float rightX, float rightY) = state.GetStick(StickInputId.Right);

                // Either stick navigates; use whichever is pushed further.
                if (Math.Abs(rightX) > Math.Abs(stickX))
                {
                    stickX = rightX;
                }

                if (Math.Abs(rightY) > Math.Abs(stickY))
                {
                    stickY = rightY;
                }

                if (direction == MoveDirection.None)
                {
                    if (state.IsPressed(GamepadButtonInputId.DpadLeft) || stickX < -StickThreshold)
                    {
                        direction = MoveDirection.Left;
                    }
                    else if (state.IsPressed(GamepadButtonInputId.DpadRight) || stickX > StickThreshold)
                    {
                        direction = MoveDirection.Right;
                    }
                    else if (state.IsPressed(GamepadButtonInputId.DpadUp) || stickY > StickThreshold)
                    {
                        direction = MoveDirection.Up;
                    }
                    else if (state.IsPressed(GamepadButtonInputId.DpadDown) || stickY < -StickThreshold)
                    {
                        direction = MoveDirection.Down;
                    }
                }

                play |= state.IsPressed(GamepadButtonInputId.A);
                options |= state.IsPressed(GamepadButtonInputId.X);
                settings |= state.IsPressed(GamepadButtonInputId.Plus);
            }

            // Directions repeat while held, like a keyboard key.
            if (direction == MoveDirection.None)
            {
                _heldDirection = MoveDirection.None;
            }
            else if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextMoveTime = now + _firstRepeatTicks;

                MoveSelection(direction);
            }
            else if (now >= _nextMoveTime)
            {
                _nextMoveTime = now + _nextRepeatTicks;

                MoveSelection(direction);
            }

            // Buttons act once per press.
            bool anyButton = play || options || settings;

            if (!anyButton)
            {
                _buttonsHeld = false;
            }
            else if (!_buttonsHeld)
            {
                _buttonsHeld = true;

                if (play)
                {
                    PlaySelected();
                }
                else if (settings)
                {
                    OpenSettings();
                }
                else
                {
                    OpenGameOptions(GameOptionsButton);
                }
            }
        }

        private void MoveSelection(MoveDirection direction)
        {
            int count = GameList.ItemCount;

            if (count == 0)
            {
                return;
            }

            int index = Math.Max(0, GameList.SelectedIndex);
            int columns = GetColumnCount();

            int target = direction switch
            {
                MoveDirection.Left => index - 1,
                MoveDirection.Right => index + 1,
                MoveDirection.Up => index - columns,
                MoveDirection.Down => Math.Min(index + columns, count - 1),
                _ => index,
            };

            // Moving down from the row above a shorter last row lands on the last tile; other moves stop at the edges.
            if (target < 0 || target >= count || (direction == MoveDirection.Down && index + columns >= count && index / columns == (count - 1) / columns))
            {
                return;
            }

            GameList.SelectedIndex = target;
            GameList.ScrollIntoView(target);
        }

        /// <summary>
        /// How many tiles fit in one row at the current window width.
        /// </summary>
        private int GetColumnCount()
        {
            if (GameList.ItemsPanelRoot is not { } panel || GameList.ContainerFromIndex(0) is not { } first)
            {
                return 1;
            }

            Thickness margin = first.Margin;
            double tileWidth = first.Bounds.Width + margin.Left + margin.Right;

            if (tileWidth <= 0 || panel.Bounds.Width <= 0)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Floor((panel.Bounds.Width + 0.5) / tileWidth));
        }

        private void RefreshGamepads(IGamepadDriver driver)
        {
            HashSet<string> present = new();

            foreach (string id in driver.GamepadsIds)
            {
                present.Add(id);
            }

            List<string> removed = new();

            foreach (string id in _gamepads.Keys)
            {
                if (!present.Contains(id))
                {
                    removed.Add(id);
                }
            }

            foreach (string id in removed)
            {
                _gamepads[id].Dispose();
                _gamepads.Remove(id);
            }

            foreach (string id in present)
            {
                if (!_gamepads.ContainsKey(id))
                {
                    IGamepad gamepad = driver.GetGamepad(id);

                    if (gamepad != null)
                    {
                        _gamepads.Add(id, gamepad);
                    }
                }
            }

            string chip = "No controller";

            if (_gamepads.Count == 1)
            {
                foreach (IGamepad gamepad in _gamepads.Values)
                {
                    chip = gamepad.Name;
                }
            }
            else if (_gamepads.Count > 1)
            {
                chip = $"{_gamepads.Count} controllers";
            }

            ControllerChip.Text = chip;
        }

        private void ReleaseGamepads()
        {
            foreach (IGamepad gamepad in _gamepads.Values)
            {
                try
                {
                    gamepad.Dispose();
                }
                catch (Exception)
                {
                }
            }

            _gamepads.Clear();
        }
    }
}
