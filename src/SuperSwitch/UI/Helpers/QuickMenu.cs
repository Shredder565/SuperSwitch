using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Systems;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Ava.UI.Windows;
using SuperSwitch.Common.Logging;
using SuperSwitch.Common.Recording;
using SuperSwitch.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;

namespace SuperSwitch.Ava.UI.Helpers
{
    /// <summary>
    /// SuperSwitch: the in-game quick menu. Pauses the game and offers Resume, recording, instant replay,
    /// full screen, Settings and Exit game. Opened with Escape, or on a controller by pressing both sticks in
    /// (or the Guide button); inside it the D-pad or a stick moves, A chooses and B resumes.
    /// </summary>
    internal static class QuickMenu
    {
        // Sticks must be pushed at least halfway before they count, the same as the home screen.
        private const float StickThreshold = 0.5f;
        private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(40);
        private static readonly long _refreshTicks = Stopwatch.Frequency * 2;
        private static readonly long _firstRepeatTicks = Stopwatch.Frequency * 400 / 1000;
        private static readonly long _nextRepeatTicks = Stopwatch.Frequency * 160 / 1000;

        private enum Choice
        {
            Resume,
            ToggleRecording,
            SaveReplay,
            ToggleFullscreen,
            Settings,
            ExitGame,
        }

        private static readonly Dictionary<string, IGamepad> _gamepads = new();
        private static MainWindow _window;
        private static DispatcherTimer _timer;
        private static long _nextGamepadRefresh;
        private static bool _isOpen;

        // State of the open menu.
        private static FAContentDialog _dialog;
        private static List<Button> _buttons;
        private static int _focusedIndex;
        private static Choice _choice;
        private static bool _buttonsHeld;
        private static int _heldMove;
        private static long _nextMoveTime;

        public static bool IsOpen => _isOpen;

        /// <summary>
        /// Starts listening for the controller shortcut that opens the menu. Call once, when the main window is ready.
        /// </summary>
        public static void Attach(MainWindow window)
        {
            _window = window;

            _timer ??= new DispatcherTimer(_pollInterval, DispatcherPriority.Input, (_, _) => Poll());
            _timer.Start();
        }

        /// <summary>
        /// Opens the quick menu over the running game.
        /// </summary>
        public static async Task ShowAsync()
        {
            MainWindow window = _window;

            if (_isOpen || window?.ViewModel is not { IsGameRunning: true } viewModel)
            {
                return;
            }

            AppHost appHost = viewModel.AppHost;

            if (appHost == null)
            {
                return;
            }

            _isOpen = true;

            bool wasPaused = viewModel.IsPaused;

            try
            {
                if (!wasPaused)
                {
                    appHost.Pause();
                }

                // Let go of the key or button that opened the menu, so it doesn't close it again straight away.
                await Task.Delay(250);

                _choice = Choice.Resume;
                _buttons = new List<Button>();
                _focusedIndex = 0;
                _buttonsHeld = true;
                _heldMove = 0;

                StackPanel panel = new()
                {
                    Spacing = 8,
                    MinWidth = 340,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                AddButton(panel, "Resume", Choice.Resume);
                AddButton(panel, GameRecorder.IsRecording ? "Stop recording" : "Start recording", Choice.ToggleRecording);

                if (GameRecorder.IsReplayBufferRunning)
                {
                    AddButton(panel, "Save instant replay", Choice.SaveReplay);
                }

                AddButton(panel, viewModel.WindowState == WindowState.FullScreen ? "Leave full screen" : "Full screen", Choice.ToggleFullscreen);
                AddButton(panel, "Settings", Choice.Settings);
                AddButton(panel, "Exit game", Choice.ExitGame);

                _dialog = new FAContentDialog
                {
                    Title = "Paused",
                    Content = panel,
                    CloseButtonText = "Resume",
                };

                _dialog.Opened += (_, _) => FocusButton(0);

                await ContentDialogHelper.ShowAsync(_dialog);
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.UI, $"The quick menu could not be shown: {ex.Message}");

                _choice = Choice.Resume;
            }
            finally
            {
                _dialog = null;
                _buttons = null;
            }

            try
            {
                switch (_choice)
                {
                    case Choice.ToggleRecording:
                        appHost.ToggleRecording();
                        break;
                    case Choice.SaveReplay:
                        appHost.SaveReplay();
                        break;
                    case Choice.ToggleFullscreen:
                        viewModel.ToggleFullscreen();
                        break;
                    case Choice.Settings:
                        await window.OpenSettingsFromHome();
                        break;
                    case Choice.ExitGame:
                        // Asks for confirmation first when that is turned on in Settings.
                        await appHost.ShowExitPrompt();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.UI, $"Quick menu action failed: {ex.Message}");
            }
            finally
            {
                // Back to the game, unless it was already paused before the menu opened or it has been closed.
                if (!wasPaused && viewModel.IsGameRunning && viewModel.IsPaused)
                {
                    appHost.Resume();
                }

                // Escape may still be held (and repeating) from closing the menu; don't let it open the menu again.
                await Task.Delay(300);

                _isOpen = false;
            }
        }

        private static void AddButton(StackPanel panel, string text, Choice choice)
        {
            Button button = new()
            {
                Content = text,
                MinHeight = 48,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };

            button.Click += (_, _) => Choose(choice);

            panel.Children.Add(button);
            _buttons.Add(button);
        }

        private static void Choose(Choice choice)
        {
            _choice = choice;
            _dialog?.Hide();
        }

        private static void FocusButton(int index)
        {
            if (_buttons == null || _buttons.Count == 0)
            {
                return;
            }

            _focusedIndex = Math.Clamp(index, 0, _buttons.Count - 1);
            _buttons[_focusedIndex].Focus();
        }

        // ---- Controller ----

        private static void Poll()
        {
            try
            {
                PollCore();
            }
            catch (Exception ex)
            {
                _timer?.Stop();

                ReleaseGamepads();

                Logger.Warning?.Print(LogClass.UI, $"The controller shortcut for the quick menu was turned off: {ex.Message}");
            }
        }

        private static void PollCore()
        {
            MainWindow window = _window;

            if (window?.ViewModel is not { IsGameRunning: true } || window.InputManager == null)
            {
                // Nothing to do outside a game; don't keep the controllers open.
                if (_gamepads.Count != 0)
                {
                    ReleaseGamepads();
                }

                _nextGamepadRefresh = 0;
                _buttonsHeld = true;

                return;
            }

            long now = Stopwatch.GetTimestamp();

            if (now >= _nextGamepadRefresh)
            {
                _nextGamepadRefresh = now + _refreshTicks;

                RefreshGamepads(window.InputManager.GamepadDriver);
            }

            bool openCombo = false;
            bool accept = false;
            bool back = false;
            int move = 0;

            foreach (IGamepad gamepad in _gamepads.Values)
            {
                GamepadStateSnapshot state = gamepad.GetStateSnapshot();

                openCombo |= state.IsPressed(GamepadButtonInputId.Guide) ||
                             (state.IsPressed(GamepadButtonInputId.LeftStick) && state.IsPressed(GamepadButtonInputId.RightStick));
                accept |= state.IsPressed(GamepadButtonInputId.A);
                back |= state.IsPressed(GamepadButtonInputId.B);

                if (move == 0)
                {
                    (_, float leftY) = state.GetStick(StickInputId.Left);
                    (_, float rightY) = state.GetStick(StickInputId.Right);

                    if (state.IsPressed(GamepadButtonInputId.DpadUp) || leftY > StickThreshold || rightY > StickThreshold)
                    {
                        move = -1;
                    }
                    else if (state.IsPressed(GamepadButtonInputId.DpadDown) || leftY < -StickThreshold || rightY < -StickThreshold)
                    {
                        move = 1;
                    }
                }
            }

            if (!_isOpen)
            {
                // In the game: only the shortcut that opens the menu matters, and only while SuperSwitch is in front.
                if (!openCombo)
                {
                    _buttonsHeld = false;
                }
                else if (!_buttonsHeld && window.IsActive)
                {
                    _buttonsHeld = true;

                    _ = ShowAsync();
                }

                return;
            }

            if (_dialog == null || _buttons == null)
            {
                return;
            }

            // Up and down repeat while held.
            if (move == 0)
            {
                _heldMove = 0;
            }
            else if (move != _heldMove)
            {
                _heldMove = move;
                _nextMoveTime = now + _firstRepeatTicks;

                FocusButton(_focusedIndex + move);
            }
            else if (now >= _nextMoveTime)
            {
                _nextMoveTime = now + _nextRepeatTicks;

                FocusButton(_focusedIndex + move);
            }

            // Buttons act once per press.
            if (!accept && !back && !openCombo)
            {
                _buttonsHeld = false;
            }
            else if (!_buttonsHeld)
            {
                _buttonsHeld = true;

                if (accept && _focusedIndex < _buttons.Count)
                {
                    // The same as clicking the highlighted button.
                    Button focused = _buttons[_focusedIndex];

                    focused.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                }
                else
                {
                    Choose(Choice.Resume);
                }
            }
        }

        private static void RefreshGamepads(IGamepadDriver driver)
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
        }

        private static void ReleaseGamepads()
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
