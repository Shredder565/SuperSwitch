using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System;
using System.IO;
using System.Runtime.Versioning;

namespace Ryujinx.Common.Configuration
{
    public static class AppDataManager
    {
        private const string DefaultBaseDir = "SuperSwitch"; // SuperSwitch: separate from any Ryubing install
        private const string DefaultPortableDir = "portable";

        // The following 3 are always part of Base Directory
        private const string GamesDir = "games";
        private const string ProfilesDir = "profiles";
        private const string KeysDir = "system";

        public enum LaunchMode
        {
            UserProfile,
            Portable,
            Custom,
        }

        public static LaunchMode Mode { get; private set; }

        public static string BaseDirPath { get; private set; }
        public static string GamesDirPath { get; private set; }
        public static string ProfilesDirPath { get; private set; }
        public static string KeysDirPath { get; private set; }
        public static string KeysDirPathUser { get; }

        public static string LogsDirPath { get; private set; }

        public const string DefaultNandDir = "bis";
        public const string DefaultSdcardDir = "sdcard";
        private const string DefaultModsDir = "mods";

        public static string CustomModsPath { get; set; }
        public static string CustomSdModsPath { get; set; }
        public static string CustomNandPath { get; set; } // TODO: Actually implement this into VFS
        public static string CustomSdCardPath { get; set; } // TODO: Actually implement this into VFS

        static AppDataManager()
        {
            KeysDirPathUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
        }

        /// <summary>
        /// SuperSwitch: name of the file holding the folder the user chose for saves, settings, keys and caches.
        /// It lives in %LocalAppData%\SuperSwitch (written by the installer and by File > Change Data Folder), so every
        /// SuperSwitch copy on this PC uses the same data. A copy next to SuperSwitch.exe, if present, overrides it.
        /// </summary>
        public const string DataLocationFileName = "DataLocation.txt";

        /// <summary>
        /// SuperSwitch: the data folder the user chose, when it could not be used at startup (for example its drive is
        /// not connected) and SuperSwitch had to fall back to <see cref="BaseDirPath"/>. Null when all is well.
        /// </summary>
        public static string UnreachableDataLocation { get; private set; }

        private static string DataLocationFileNextToExe => Path.Combine(AppContext.BaseDirectory, DataLocationFileName);

        private static string DataLocationFileUser =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DefaultBaseDir, DataLocationFileName);

        /// <summary>
        /// The folder the user chose for SuperSwitch's data, or null when none was chosen.
        /// </summary>
        public static string ReadDataLocation()
        {
            foreach (string file in new[] { DataLocationFileNextToExe, DataLocationFileUser })
            {
                try
                {
                    if (File.Exists(file))
                    {
                        string path = File.ReadAllText(file).Trim().Trim('"');

                        if (path.Length != 0)
                        {
                            return Environment.ExpandEnvironmentVariables(path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning?.Print(LogClass.Application, $"Could not read {file}: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Remembers the folder for SuperSwitch's data (takes effect on the next start).
        /// </summary>
        public static void SaveDataLocation(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataLocationFileUser));
            File.WriteAllText(DataLocationFileUser, path);

            // Keep an overriding copy next to the program in step, if there is one.
            if (File.Exists(DataLocationFileNextToExe))
            {
                try
                {
                    File.WriteAllText(DataLocationFileNextToExe, path);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    Logger.Warning?.Print(LogClass.Application, $"Could not update {DataLocationFileNextToExe}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Copies all of SuperSwitch's data to another folder. Files already there are only replaced by newer ones.
        /// Returns how many files were copied and how many could not be (for example because they were in use).
        /// </summary>
        public static (int Copied, int Failed) CopyData(string sourceDir, string targetDir)
        {
            int copied = 0;
            int failed = 0;

            sourceDir = Path.GetFullPath(sourceDir);
            targetDir = Path.GetFullPath(targetDir);

            foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, file);

                if (relative.StartsWith("Logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string destination = Path.Combine(targetDir, relative);

                try
                {
                    if (File.Exists(destination) && File.GetLastWriteTimeUtc(destination) >= File.GetLastWriteTimeUtc(file))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination, true);
                    copied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.Warning?.Print(LogClass.Application, $"Could not copy {relative}: {ex.Message}");
                }
            }

            return (copied, failed);
        }

        public static void Initialize(string baseDirPath)
        {
            // SuperSwitch: use the data folder the user chose (installer or File > Change Data Folder), unless one was given on the command line.
            bool fromDataLocation = false;

            if (baseDirPath == null)
            {
                baseDirPath = ReadDataLocation();
                fromDataLocation = baseDirPath != null;

                if (baseDirPath != null)
                {
                    try
                    {
                        Directory.CreateDirectory(baseDirPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error?.Print(LogClass.Application, $"Could not create the data folder '{baseDirPath}': {ex.Message}");
                    }
                }
            }

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (appDataPath.Length == 0)
            {
                appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            string userProfilePath = Path.Combine(appDataPath, DefaultBaseDir);
            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultPortableDir);

            // On macOS, check for a portable directory next to the app bundle as well.
            if (OperatingSystem.IsMacOS() && !Directory.Exists(portablePath))
            {
                string bundlePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
                // Make sure we're actually running within an app bundle.
                if (bundlePath.EndsWith(".app"))
                {
                    portablePath = Path.GetFullPath(Path.Combine(bundlePath, "..", DefaultPortableDir));
                }
            }

            if (Directory.Exists(portablePath))
            {
                BaseDirPath = portablePath;
                Mode = LaunchMode.Portable;
            }
            else
            {
                BaseDirPath = userProfilePath;
                Mode = LaunchMode.UserProfile;
            }

            if (baseDirPath != null && baseDirPath != userProfilePath)
            {
                if (!Directory.Exists(baseDirPath))
                {
                    Logger.Error?.Print(LogClass.Application, $"Custom Data Directory '{baseDirPath}' does not exist. Falling back to {Mode}...");

                    if (fromDataLocation)
                    {
                        // Shown to the user at startup: running on an empty fallback folder looks like lost saves.
                        UnreachableDataLocation = baseDirPath;
                    }
                }
                else
                {
                    BaseDirPath = baseDirPath;
                    Mode = LaunchMode.Custom;
                }
            }

            BaseDirPath = Path.GetFullPath(BaseDirPath); // convert relative paths

            if (IsPathSymlink(BaseDirPath))
            {
                Logger.Warning?.Print(LogClass.Application, "Application data directory is a symlink. This may be unintended.");
            }

            SetupBasePaths();
        }

        public static string GetOrCreateLogsDir()
        {
            if (Directory.Exists(LogsDirPath))
            {
                return LogsDirPath;
            }

            Logger.Notice.Print(LogClass.Application, "Logging directory not found; attempting to create new logging directory.");
            LogsDirPath = SetUpLogsDir();

            return LogsDirPath;
        }

        private static string SetUpLogsDir()
        {
            string logDir = string.Empty;

            if (Mode == LaunchMode.Portable)
            {
                logDir = Path.Combine(BaseDirPath, "Logs");
                try
                {
                    Directory.CreateDirectory(logDir);
                }
                catch
                {
                    Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                    return null;
                }
            }
            else
            {
                if (OperatingSystem.IsMacOS())
                {
                    // NOTE: Should evaluate to "~/Library/Logs/Ryujinx/".
                    logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", DefaultBaseDir);
                    try
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");
                        logDir = string.Empty;
                    }

                    if (string.IsNullOrEmpty(logDir))
                    {
                        // NOTE: Should evaluate to "~/Library/Application Support/Ryujinx/Logs".
                        logDir = Path.Combine(BaseDirPath, "Logs");

                        try
                        {
                            Directory.CreateDirectory(logDir);
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                            return null;
                        }
                    }
                }
                else if (OperatingSystem.IsWindows())
                {
                    // NOTE: Should evaluate to a "Logs" directory in whatever directory Ryujinx was launched from.
                    logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    try
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");
                        logDir = string.Empty;
                    }

                    if (string.IsNullOrEmpty(logDir))
                    {
                        // NOTE: Should evaluate to "C:\Users\user\AppData\Roaming\Ryujinx\Logs".
                        logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir, "Logs");

                        try
                        {
                            Directory.CreateDirectory(logDir);
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                            return null;
                        }
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    // NOTE: Should evaluate to "~/.config/Ryujinx/Logs".
                    logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir, "Logs");

                    try
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                        return null;
                    }
                }
            }

            return logDir;
        }

        private static void SetupBasePaths()
        {
            Directory.CreateDirectory(BaseDirPath);
            LogsDirPath = SetUpLogsDir();
            Directory.CreateDirectory(GamesDirPath = Path.Combine(BaseDirPath, GamesDir));
            Directory.CreateDirectory(ProfilesDirPath = Path.Combine(BaseDirPath, ProfilesDir));
            Directory.CreateDirectory(KeysDirPath = Path.Combine(BaseDirPath, KeysDir));
        }

        // Check if existing old baseDirPath is a symlink, to prevent possible errors.
        // Should be removed, when the existence of the old directory isn't checked anymore.
        private static bool IsPathSymlink(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
            }
            catch
            {
                return false;
            }
        }

        [SupportedOSPlatform("macos")]
        public static void FixMacOSConfigurationFolders()
        {
            string oldConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", DefaultBaseDir);
            if (Path.Exists(oldConfigPath) && !IsPathSymlink(oldConfigPath) && !Path.Exists(BaseDirPath))
            {
                FileSystemUtils.MoveDirectory(oldConfigPath, BaseDirPath);
                Directory.CreateSymbolicLink(oldConfigPath, BaseDirPath);
            }

            string correctApplicationDataDirectoryPath =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir);
            if (IsPathSymlink(correctApplicationDataDirectoryPath))
            {
                //copy the files somewhere temporarily
                string tempPath = Path.Combine(Path.GetTempPath(), DefaultBaseDir);
                try
                {
                    FileSystemUtils.CopyDirectory(correctApplicationDataDirectoryPath, tempPath, true);
                }
                catch (Exception exception)
                {
                    Logger.Error?.Print(LogClass.Application,
                        $"Critical error copying Ryujinx application data into the temp folder. {exception}");
                    try
                    {
                        FileSystemInfo resolvedDirectoryInfo =
                            Directory.ResolveLinkTarget(correctApplicationDataDirectoryPath, true);
                        string resolvedPath = resolvedDirectoryInfo.FullName;
                        Logger.Error?.Print(LogClass.Application, $"Please manually move your Ryujinx data from {resolvedPath} to {correctApplicationDataDirectoryPath}, and remove the symlink.");
                    }
                    catch (Exception symlinkException)
                    {
                        Logger.Error?.Print(LogClass.Application, $"Unable to resolve the symlink for Ryujinx application data: {symlinkException}. Follow the symlink at {correctApplicationDataDirectoryPath} and move your data back to the Application Support folder.");
                    }

                    return;
                }

                //delete the symlink
                try
                {
                    //This will fail if this is an actual directory, so there is no way we can actually delete user data here.
                    File.Delete(correctApplicationDataDirectoryPath);
                }
                catch (Exception exception)
                {
                    Logger.Error?.Print(LogClass.Application,
                        $"Critical error deleting the Ryujinx application data folder symlink at {correctApplicationDataDirectoryPath}. {exception}");
                    try
                    {
                        FileSystemInfo resolvedDirectoryInfo =
                            Directory.ResolveLinkTarget(correctApplicationDataDirectoryPath, true);
                        string resolvedPath = resolvedDirectoryInfo.FullName;
                        Logger.Error?.Print(LogClass.Application, $"Please manually move your Ryujinx data from {resolvedPath} to {correctApplicationDataDirectoryPath}, and remove the symlink.");
                    }
                    catch (Exception symlinkException)
                    {
                        Logger.Error?.Print(LogClass.Application, $"Unable to resolve the symlink for Ryujinx application data: {symlinkException}. Follow the symlink at {correctApplicationDataDirectoryPath} and move your data back to the Application Support folder.");
                    }

                    return;
                }

                //put the files back
                try
                {
                    FileSystemUtils.CopyDirectory(tempPath, correctApplicationDataDirectoryPath, true);
                }
                catch (Exception exception)
                {
                    Logger.Error?.Print(LogClass.Application,
                        $"Critical error copying Ryujinx application data into the correct location. {exception}. Please manually move your application data from {tempPath} to {correctApplicationDataDirectoryPath}.");
                }
            }
        }

        public static string GetModsPath() => CustomModsPath ?? Directory.CreateDirectory(Path.Combine(BaseDirPath, DefaultModsDir)).FullName;
        public static string GetSdModsPath() => CustomSdModsPath ?? Directory.CreateDirectory(Path.Combine(BaseDirPath, DefaultSdcardDir, "atmosphere")).FullName;
    }
}
