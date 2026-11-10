using LibHac.Tools.FsSystem;
using SuperSwitch.Audio.Backends.SDL3;
using SuperSwitch.Ava;
using SuperSwitch.Ava.Systems;
using SuperSwitch.Ava.Systems.Configuration;
using SuperSwitch.Common.Configuration;
using SuperSwitch.Common.Configuration.Hid;
using SuperSwitch.Common.Configuration.Hid.Controller;
using SuperSwitch.Common.Logging;
using SuperSwitch.Common.Utilities;
using SuperSwitch.Cpu;
using SuperSwitch.Graphics.GAL;
using SuperSwitch.Graphics.Vulkan;
using SuperSwitch.HLE;
using SuperSwitch.Input;
using SuperSwitch.Input.SDL3;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SuperSwitch.Headless
{
    public partial class HeadlessSuperSwitch
    {
        public static void Initialize()
        {
            // Hook unhandled exception and process exit events.
            AppDomain.CurrentDomain.UnhandledException += (sender, e)
                => Program.ProcessUnhandledException(sender, e.ExceptionObject as Exception, e.IsTerminating);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Program.Exit();

            // Initialize the configuration.
            ConfigurationState.Initialize();

            // Logging system information.
            Program.PrintSystemInfo();
        }

        private static InputConfig HandlePlayerConfiguration(string inputProfileName, string inputId, PlayerIndex index)
        {
            if (inputId == null)
            {
                if (index == PlayerIndex.Player1)
                {
                    Logger.Info?.Print(LogClass.Application, $"{index} not configured, defaulting to default keyboard.");

                    // Default to keyboard
                    inputId = "0";
                }
                else
                {
                    Logger.Info?.Print(LogClass.Application, $"{index} not configured");

                    return null;
                }
            }

            IGamepad gamepad = _inputManager.KeyboardDriver.GetGamepad(inputId);

            bool isKeyboard = true;

            if (gamepad == null)
            {
                gamepad = _inputManager.GamepadDriver.GetGamepad(inputId);
                isKeyboard = false;

                if (gamepad == null)
                {
                    Logger.Error?.Print(LogClass.Application, $"{index} gamepad not found (\"{inputId}\")");

                    return null;
                }
            }

            string gamepadName = gamepad.Name;
            
            bool isNintendoStyle = false;
            
            if (gamepad is SDL3Gamepad sdlGp)
            {
                // Nintendo vendor ID is 0x057E
                isNintendoStyle = sdlGp.VendorId == 0x057E;
            }
            else
            {
                // Fallback to name-based detection
                isNintendoStyle = gamepadName.Contains("Nintendo", StringComparison.OrdinalIgnoreCase);
            }

            gamepad.Dispose();

            InputConfig config;

            if (inputProfileName == null || inputProfileName.Equals("default"))
            {
                if (isKeyboard)
                {
                    config = InputConfigDefaults.CreateDefaultKeyboardConfiguration(
                        null,
                        null,
                        ControllerType.JoyconPair,
                        index);
                }
                else
                {

                    config = InputConfigDefaults.CreateDefaultControllerConfiguration(
                        null,
                        null,
                        ControllerType.JoyconPair,
                        index,
                        isNintendoStyle);
                }
            }
            else
            {
                string profileBasePath;

                if (isKeyboard)
                {
                    profileBasePath = Path.Combine(AppDataManager.ProfilesDirPath, "keyboard");
                }
                else
                {
                    profileBasePath = Path.Combine(AppDataManager.ProfilesDirPath, "controller");
                }

                string path = Path.Combine(profileBasePath, inputProfileName + ".json");

                if (!File.Exists(path))
                {
                    Logger.Error?.Print(LogClass.Application, $"Input profile \"{inputProfileName}\" not found for \"{inputId}\"");

                    return null;
                }

                try
                {
                    config = JsonHelper.DeserializeFromFile(path, _serializerContext.InputConfig);
                }
                catch (JsonException)
                {
                    Logger.Error?.Print(LogClass.Application, $"Input profile \"{inputProfileName}\" parsing failed for \"{inputId}\"");

                    return null;
                }
            }

            config.Id = inputId;
            config.PlayerIndex = index;

            string inputTypeName = isKeyboard ? "Keyboard" : "Gamepad";

            Logger.Info?.Print(LogClass.Application, $"{config.PlayerIndex} configured with {inputTypeName} \"{config.Id}\"");

            // If both stick ranges are 0 (usually indicative of an outdated profile load) then both sticks will be set to 1.0.
            if (config is StandardControllerInputConfig controllerConfig)
            {
                if (controllerConfig.RangeLeft <= 0.0f && controllerConfig.RangeRight <= 0.0f)
                {
                    controllerConfig.RangeLeft = 1.0f;
                    controllerConfig.RangeRight = 1.0f;

                    Logger.Info?.Print(LogClass.Application, $"{config.PlayerIndex} stick range reset. Save the profile now to update your configuration");
                }
            }

            return config;
        }

        private static IRenderer CreateRenderer(Options options, WindowBase window)
        {
            if (options.GraphicsBackend == GraphicsBackend.Vulkan && window is VulkanWindow vulkanWindow)
            {
                string preferredGpuId = string.Empty;
                Vk api = Vk.GetApi();

                if (!string.IsNullOrEmpty(options.PreferredGPUVendor))
                {
                    string preferredGpuVendor = options.PreferredGPUVendor.ToLowerInvariant();
                    DeviceInfo[] devices = VulkanRenderer.GetPhysicalDevices(api);

                    foreach (DeviceInfo device in devices)
                    {
                        if (device.Vendor.Equals(preferredGpuVendor, StringComparison.OrdinalIgnoreCase))
                        {
                            preferredGpuId = device.Id;
                            break;
                        }
                    }
                }

                return new VulkanRenderer(
                    api,
                    (instance, vk) => new SurfaceKHR((ulong)vulkanWindow.CreateWindowSurface(instance.Handle)),
                    VulkanWindow.GetRequiredInstanceExtensions,
                    preferredGpuId);
            }

            throw new NotSupportedException("SuperSwitch only supports the Vulkan graphics backend.");
        }

        private static Switch InitializeEmulationContext(WindowBase window, IRenderer renderer, Options options) =>
            new(
                new HleConfiguration(
                        options.DramSize,
                        options.SystemLanguage,
                        options.SystemRegion,
                        options.VSyncMode,
                        !options.DisableDockedMode,
                        !options.DisablePTC,
                        ITickSource.RealityTickScalar,
                        options.EnableInternetAccess,
                        !options.DisableFsIntegrityChecks ? IntegrityCheckLevel.ErrorOnInvalid : IntegrityCheckLevel.None,
                        options.FsGlobalAccessLogMode,
                        options.SystemTimeOffset,
                        options.SystemTimeZone,
                        options.MemoryManagerMode,
                        options.IgnoreMissingServices,
                        options.AspectRatio,
                        options.AudioVolume,
                        options.UseHypervisor ?? true,
                        options.MultiplayerLanInterfaceId,
                        Common.Configuration.Multiplayer.MultiplayerMode.Disabled,
                        false,
                        string.Empty,
                        string.Empty,
                        options.EnableGdbStub,
                        options.GdbStubPort,
                        options.DebuggerSuspendOnStart,
                        options.CustomVSyncInterval
                    )
                    .Configure(
                        _virtualFileSystem,
                        _libHacHorizonManager,
                        _contentManager,
                        _accountManager,
                        _userChannelPersistence,
                        renderer.TryMakeThreaded(options.BackendThreading),
                        new SDL3HardwareDeviceDriver(),
                        window
                    )
            );
    }
}
