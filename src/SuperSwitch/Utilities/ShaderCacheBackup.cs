using SuperSwitch.Common.Configuration;
using SuperSwitch.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SuperSwitch.Ava.Utilities
{
    /// <summary>
    /// SuperSwitch: keeps a copy of each game's shader cache in a backup folder (for example OneDrive),
    /// and restores it automatically when the local cache is missing (after a reformat or reinstall) or older.
    /// </summary>
    internal static class ShaderCacheBackup
    {
        private static readonly object _lock = new();

        // Cloud folders and different file systems don't keep modification times to the exact tick.
        private static readonly TimeSpan _timeTolerance = TimeSpan.FromSeconds(2);

        // Games whose backup could not be restored at startup this session (guarded by _lock).
        private static readonly HashSet<string> _restoreFailed = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The configured backup folder, or the default one when none is set.
        /// </summary>
        public static string ResolveBackupRoot(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            }

            return DefaultBackupRoot;
        }

        /// <summary>
        /// OneDrive (so it survives a reformat) when available, otherwise Documents.
        /// </summary>
        public static string DefaultBackupRoot
        {
            get
            {
                string oneDrive = Environment.GetEnvironmentVariable("OneDrive");

                string root = !string.IsNullOrEmpty(oneDrive) && Directory.Exists(oneDrive)
                    ? oneDrive
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                return Path.Combine(root, "SuperSwitch Shader Cache Backup");
            }
        }

        private static string LocalShaderDir(string titleId)
        {
            return Path.Combine(AppDataManager.GamesDirPath, titleId.ToLowerInvariant(), "cache", "shader");
        }

        private static string BackupShaderDir(string backupRoot, string titleId)
        {
            return Path.Combine(backupRoot, titleId.ToLowerInvariant(), "shader");
        }

        /// <summary>
        /// Brings back the backed up cache when the local one is missing (reformat, reinstall, new PC), or when the backup
        /// is both more recent and at least as big (another PC played this game since and compiled more shaders).
        /// A local cache that is newer than the backup is never replaced, however small it is.
        /// Must run before the game's shader cache is opened.
        /// </summary>
        public static void RestoreIfNewer(string backupRoot, string titleId)
        {
            if (string.IsNullOrEmpty(titleId))
            {
                return;
            }

            try
            {
                lock (_lock)
                {
                    _restoreFailed.Remove(titleId);

                    string source = BackupShaderDir(backupRoot, titleId);
                    string target = LocalShaderDir(titleId);

                    RepairInterruptedSwap(source);

                    if (!Directory.Exists(source))
                    {
                        return;
                    }

                    Snapshot backup = Snapshot.Take(source);

                    if (backup.Files.Count == 0)
                    {
                        return;
                    }

                    Snapshot local = Directory.Exists(target) ? Snapshot.Take(target) : Snapshot.Empty;

                    bool localMissing = local.Files.Count == 0;
                    bool backupIsNewerAndNotSmaller =
                        backup.Newest > local.Newest + _timeTolerance && backup.Size >= local.Size;

                    if (!localMissing && (backup.SameAs(local) || !backupIsNewerAndNotSmaller))
                    {
                        return;
                    }

                    // Replace the whole cache so the table of contents and data files always match.
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }

                    int files = CopyDirectory(source, target);

                    Logger.Info?.Print(LogClass.Application,
                        $"Restored shader cache for {titleId} from backup ({files} files, {backup.Size / (1024 * 1024)} MiB).", "ShaderCacheBackup");
                }
            }
            catch (Exception e)
            {
                // Remember the failure: the backup must not be replaced by whatever small cache this session builds instead.
                lock (_lock)
                {
                    _restoreFailed.Add(titleId);
                }

                Logger.Warning?.Print(LogClass.Application, $"Could not restore the shader cache backup: {e.Message}", "ShaderCacheBackup");
            }
        }

        /// <summary>
        /// Copies the local shader cache to the backup folder on a separate thread, after the game has closed.
        /// The thread is not a background thread, so closing SuperSwitch right away still finishes the copy.
        /// </summary>
        public static void BackupInBackground(string backupRoot, string titleId)
        {
            if (string.IsNullOrEmpty(titleId))
            {
                return;
            }

            Thread thread = new(() => Backup(backupRoot, titleId))
            {
                Name = "SuperSwitch.ShaderCacheBackup",
                IsBackground = false,
                Priority = ThreadPriority.BelowNormal,
            };

            thread.Start();
        }

        /// <summary>
        /// Removes the backup of a game's shader cache. Called when the user purges the cache,
        /// so the purged cache doesn't simply come back from the backup on the next launch.
        /// </summary>
        public static void DeleteBackup(string backupRoot, string titleId)
        {
            if (string.IsNullOrEmpty(titleId))
            {
                return;
            }

            try
            {
                lock (_lock)
                {
                    string target = BackupShaderDir(backupRoot, titleId);

                    foreach (string dir in new[] { target, target + ".new", target + ".old" })
                    {
                        if (Directory.Exists(dir))
                        {
                            Directory.Delete(dir, true);
                        }
                    }

                    Logger.Info?.Print(LogClass.Application, $"Removed the shader cache backup for {titleId}.", "ShaderCacheBackup");
                }
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Could not remove the shader cache backup: {e.Message}", "ShaderCacheBackup");
            }
        }

        private static void Backup(string backupRoot, string titleId)
        {
            try
            {
                lock (_lock)
                {
                    if (_restoreFailed.Contains(titleId))
                    {
                        Logger.Info?.Print(LogClass.Application,
                            $"Shader cache backup for {titleId} skipped: the existing backup could not be restored at startup and is kept as it is.", "ShaderCacheBackup");

                        return;
                    }

                    string source = LocalShaderDir(titleId);
                    string target = BackupShaderDir(backupRoot, titleId);

                    if (!Directory.Exists(source))
                    {
                        return;
                    }

                    RepairInterruptedSwap(target);

                    Snapshot local = Snapshot.Take(source);

                    if (local.Files.Count == 0)
                    {
                        return;
                    }

                    if (Directory.Exists(target))
                    {
                        Snapshot backup = Snapshot.Take(target);

                        // Nothing new was compiled this session: don't copy (and make OneDrive upload) the whole cache again.
                        if (backup.SameAs(local))
                        {
                            return;
                        }

                        // Another PC backed up a more recent cache while this game was running; leave it alone.
                        if (backup.Newest > local.Newest + _timeTolerance)
                        {
                            return;
                        }
                    }

                    // Copy to a temporary folder first, then swap, so a backup is never left half written.
                    string temp = target + ".new";
                    string old = target + ".old";

                    if (Directory.Exists(temp))
                    {
                        Directory.Delete(temp, true);
                    }

                    int files = CopyDirectory(source, temp);

                    if (Directory.Exists(old))
                    {
                        Directory.Delete(old, true);
                    }

                    if (Directory.Exists(target))
                    {
                        Directory.Move(target, old);
                    }

                    Directory.Move(temp, target);

                    if (Directory.Exists(old))
                    {
                        Directory.Delete(old, true);
                    }

                    Logger.Info?.Print(LogClass.Application,
                        $"Backed up shader cache for {titleId} ({files} files, {local.Size / (1024 * 1024)} MiB) to {target}.", "ShaderCacheBackup");
                }
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Could not back up the shader cache: {e.Message}", "ShaderCacheBackup");
            }
        }

        /// <summary>
        /// If an earlier backup was interrupted between moving the old backup away and moving the new one in,
        /// put the old backup back.
        /// </summary>
        private static void RepairInterruptedSwap(string backupDir)
        {
            string old = backupDir + ".old";

            if (!Directory.Exists(backupDir) && Directory.Exists(old))
            {
                Directory.Move(old, backupDir);
            }
        }

        /// <summary>
        /// The files of a cache folder (relative path, size, time), used to tell whether two copies are the same
        /// and which one is more recent. File.Copy keeps the modification time, so an unchanged copy compares equal.
        /// </summary>
        private sealed class Snapshot
        {
            public static readonly Snapshot Empty = new();

            public readonly Dictionary<string, (long Size, DateTime Time)> Files = new(StringComparer.OrdinalIgnoreCase);
            public long Size;
            public DateTime Newest = DateTime.MinValue;

            public static Snapshot Take(string root)
            {
                Snapshot snapshot = new();

                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    FileInfo info = new(file);
                    DateTime time = info.LastWriteTimeUtc;

                    snapshot.Files[Path.GetRelativePath(root, file)] = (info.Length, time);
                    snapshot.Size += info.Length;

                    if (time > snapshot.Newest)
                    {
                        snapshot.Newest = time;
                    }
                }

                return snapshot;
            }

            public bool SameAs(Snapshot other)
            {
                if (Files.Count != other.Files.Count)
                {
                    return false;
                }

                foreach ((string name, (long size, DateTime time)) in Files)
                {
                    if (!other.Files.TryGetValue(name, out (long Size, DateTime Time) match) ||
                        match.Size != size ||
                        (match.Time - time).Duration() > _timeTolerance)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static int CopyDirectory(string source, string target)
        {
            int count = 0;

            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, file));

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
                count++;
            }

            return count;
        }
    }
}
