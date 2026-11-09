using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuperSwitch.VideoConverter
{
    /// <summary>
    /// SuperSwitch Video Converter: re-encodes videos with the recorder's NVIDIA (NVENC) presets.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private static readonly string[] _videoExtensions =
        [
            ".mp4", ".mkv", ".mov", ".avi", ".webm", ".ts", ".m2ts", ".mts", ".flv", ".wmv", ".m4v", ".mpg", ".mpeg",
        ];

        private sealed class Job
        {
            public string Input;
            public string Output;
            public bool Done;
        }

        private readonly ListView _list;
        private readonly ComboBox _quality;
        private readonly ComboBox _codec;
        private readonly TextBox _outputFolder;
        private readonly ProgressBar _progress;
        private readonly Label _status;
        private readonly Button _convert;
        private readonly Button _cancel;
        private readonly Button _add;
        private readonly Button _remove;
        private readonly Button _clear;
        private readonly Button _browse;

        private CancellationTokenSource _cts;
        private string _ffmpeg;

        public MainForm()
        {
            Text = "SuperSwitch Video Converter";
            Width = 900;
            Height = 600;
            MinimumSize = new Size(700, 450);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            AllowDrop = true;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception)
            {
                // Default icon.
            }

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10),
            };

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // File buttons.
            FlowLayoutPanel fileButtons = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            _add = new Button { Text = "Add videos...", AutoSize = true };
            _remove = new Button { Text = "Remove selected", AutoSize = true };
            _clear = new Button { Text = "Clear list", AutoSize = true };
            Label hint = new()
            {
                Text = "or drag videos (or folders) onto this window",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(8, 7, 0, 0),
            };

            fileButtons.Controls.AddRange([_add, _remove, _clear, hint]);

            // File list.
            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                AllowDrop = true,
            };

            _list.Columns.Add("Video", 560);
            _list.Columns.Add("Status", 260);

            // Options.
            TableLayoutPanel options = new()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(0, 8, 0, 4),
            };

            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _quality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _quality.Items.AddRange(EncodePresets.QualityNames);
            _quality.SelectedIndex = 0;

            _codec = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _codec.Items.AddRange(EncodePresets.CodecNames);
            _codec.SelectedIndex = 0;

            _outputFolder = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Same folder as each video" };
            _browse = new Button { Text = "Browse...", AutoSize = true };

            options.Controls.Add(MakeLabel("Quality:"), 0, 0);
            options.Controls.Add(_quality, 1, 0);
            options.Controls.Add(MakeLabel("Codec:"), 0, 1);
            options.Controls.Add(_codec, 1, 1);
            options.Controls.Add(MakeLabel("Save to:"), 0, 2);
            options.Controls.Add(_outputFolder, 1, 2);
            options.Controls.Add(_browse, 2, 2);

            // Progress and actions.
            TableLayoutPanel bottom = new()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
            };

            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, Height = 24 };
            _convert = new Button { Text = "Convert", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            _cancel = new Button { Text = "Cancel", AutoSize = true, Enabled = false };
            _status = new Label { AutoSize = true, Text = "Add videos to convert.", Padding = new Padding(0, 4, 0, 0) };

            bottom.Controls.Add(_progress, 0, 0);
            bottom.Controls.Add(_convert, 1, 0);
            bottom.Controls.Add(_cancel, 2, 0);
            bottom.Controls.Add(_status, 0, 1);
            bottom.SetColumnSpan(_status, 3);

            root.Controls.Add(fileButtons, 0, 0);
            root.Controls.Add(_list, 0, 1);
            root.Controls.Add(options, 0, 2);
            root.Controls.Add(bottom, 0, 3);

            Controls.Add(root);

            AcceptButton = _convert;

            _add.Click += (_, _) => BrowseForVideos();
            _remove.Click += (_, _) => RemoveSelected();
            _clear.Click += (_, _) => ClearList();
            _browse.Click += (_, _) => BrowseForOutputFolder();
            _convert.Click += async (_, _) => await ConvertAllAsync();
            _cancel.Click += (_, _) => _cts?.Cancel();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            _list.DragEnter += OnDragEnter;
            _list.DragDrop += OnDragDrop;

            FormClosing += OnFormClosing;
        }

        private static Label MakeLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 8, 0) };
        }

        public void AddFiles(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    IEnumerable<string> files;

                    try
                    {
                        files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                            .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    AddFiles(files);
                }
                else if (File.Exists(path))
                {
                    bool exists = _list.Items.Cast<ListViewItem>()
                        .Any(i => string.Equals(((Job)i.Tag).Input, path, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        continue;
                    }

                    ListViewItem item = new(path) { Tag = new Job { Input = path } };
                    item.SubItems.Add("Waiting");

                    _list.Items.Add(item);
                }
            }

            UpdateIdleStatus();
        }

        private void UpdateIdleStatus()
        {
            if (_cts != null)
            {
                return;
            }

            int waiting = _list.Items.Cast<ListViewItem>().Count(i => !((Job)i.Tag).Done);

            _status.Text = waiting == 0
                ? "Add videos to convert."
                : $"{waiting} video(s) ready. Press Convert.";
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) && _cts == null
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (_cts == null && e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            {
                AddFiles(paths);
            }
        }

        private void BrowseForVideos()
        {
            using OpenFileDialog dialog = new()
            {
                Title = "Choose videos to convert",
                Multiselect = true,
                Filter = "Videos|" + string.Join(";", _videoExtensions.Select(e => "*" + e)) + "|All files|*.*",
            };

            string videos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "SuperSwitch");

            dialog.InitialDirectory = Directory.Exists(videos) ? videos : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void BrowseForOutputFolder()
        {
            using FolderBrowserDialog dialog = new() { Description = "Save converted videos to", UseDescriptionForTitle = true };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _outputFolder.Text = dialog.SelectedPath;
            }
        }

        private void RemoveSelected()
        {
            foreach (ListViewItem item in _list.SelectedItems.Cast<ListViewItem>().ToList())
            {
                _list.Items.Remove(item);
            }

            UpdateIdleStatus();
        }

        private void ClearList()
        {
            _list.Items.Clear();
            UpdateIdleStatus();
        }

        private void SetBusy(bool busy)
        {
            _convert.Enabled = !busy;
            _cancel.Enabled = busy;
            _add.Enabled = !busy;
            _remove.Enabled = !busy;
            _clear.Enabled = !busy;
            _browse.Enabled = !busy;
            _quality.Enabled = !busy;
            _codec.Enabled = !busy;
            _outputFolder.Enabled = !busy;
        }

        private async Task ConvertAllAsync()
        {
            if (_cts != null)
            {
                return;
            }

            List<ListViewItem> items = _list.Items.Cast<ListViewItem>().Where(i => !((Job)i.Tag).Done).ToList();

            if (items.Count == 0)
            {
                MessageBox.Show(this, "Add some videos first (button or drag and drop).", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _ffmpeg ??= FindFfmpeg();

            if (_ffmpeg == null)
            {
                MessageBox.Show(this,
                    "FFmpeg was not found.\n\nInstall it from PowerShell with:\n    winget install Gyan.FFmpeg\nthen restart this program.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputFolder = _outputFolder.Text.Trim();

            if (outputFolder.Length > 0)
            {
                try
                {
                    Directory.CreateDirectory(outputFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Can't use the output folder:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int quality = _quality.SelectedIndex;
            bool hevc = _codec.SelectedIndex == 0;

            _cts = new CancellationTokenSource();
            SetBusy(true);

            int converted = 0;
            int failed = 0;
            long savedBytes = 0;

            try
            {
                for (int index = 0; index < items.Count; index++)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }

                    ListViewItem item = items[index];
                    Job job = (Job)item.Tag;

                    if (!File.Exists(job.Input))
                    {
                        item.SubItems[1].Text = "File not found";
                        failed++;
                        continue;
                    }

                    job.Output = GetOutputPath(job.Input, outputFolder, hevc);

                    string prefix = $"Video {index + 1} of {items.Count}: {Path.GetFileName(job.Input)}";

                    item.SubItems[1].Text = "Converting...";
                    item.EnsureVisible();
                    _progress.Value = 0;
                    _status.Text = prefix;

                    Stopwatch watch = Stopwatch.StartNew();

                    (int exitCode, string errors) = await RunFfmpegAsync(job.Input, job.Output, quality, hevc, fraction =>
                    {
                        int value = (int)Math.Clamp(fraction * 1000, 0, 1000);

                        _progress.Value = value;
                        item.SubItems[1].Text = $"Converting... {value / 10.0:F1}%";

                        if (fraction > 0.01)
                        {
                            TimeSpan remaining = TimeSpan.FromSeconds(watch.Elapsed.TotalSeconds * (1 - fraction) / fraction);
                            _status.Text = $"{prefix} - about {FormatTime(remaining)} left";
                        }
                    }, _cts.Token);

                    if (_cts.IsCancellationRequested)
                    {
                        TryDelete(job.Output);
                        item.SubItems[1].Text = "Cancelled";
                        break;
                    }

                    if (exitCode == 0 && File.Exists(job.Output))
                    {
                        long before = new FileInfo(job.Input).Length;
                        long after = new FileInfo(job.Output).Length;

                        savedBytes += before - after;
                        job.Done = true;
                        converted++;

                        item.SubItems[1].Text = $"Done: {FormatSize(after)} ({(before > 0 ? after * 100.0 / before : 0):F0}% of original)";
                    }
                    else
                    {
                        TryDelete(job.Output);
                        failed++;
                        item.SubItems[1].Text = "Failed (double-click for details)";
                        item.ToolTipText = errors;
                        item.SubItems[0].Tag = errors;
                        _list.ShowItemToolTips = true;
                    }
                }
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                SetBusy(false);
                _progress.Value = 0;
            }

            string summary = $"Finished: {converted} converted";

            if (failed > 0)
            {
                summary += $", {failed} failed";
            }

            if (savedBytes > 0)
            {
                summary += $", {FormatSize(savedBytes)} saved";
            }

            _status.Text = summary + ".";
        }

        private async Task<(int ExitCode, string Errors)> RunFfmpegAsync(
            string input,
            string output,
            int quality,
            bool hevc,
            Action<double> onProgress,
            CancellationToken token)
        {
            string args =
                "-hide_banner -y -hwaccel auto " +
                $"-i \"{input}\" " +
                "-map 0:v:0 -map 0:a? -pix_fmt yuv420p " +
                EncodePresets.GetVideoArgs(quality, hevc) +
                "-c:a aac -b:a 256k -movflags +faststart " +
                "-progress pipe:1 -nostats " +
                $"\"{output}\"";

            ProcessStartInfo startInfo = new(_ffmpeg, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process process = new() { StartInfo = startInfo };

            double durationSeconds = 0;
            Queue<string> lastErrors = new();
            Regex durationRegex = new(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                if (durationSeconds == 0)
                {
                    Match match = durationRegex.Match(e.Data);

                    if (match.Success)
                    {
                        durationSeconds =
                            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 +
                            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60 +
                            double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                    }
                }

                lock (lastErrors)
                {
                    lastErrors.Enqueue(e.Data);

                    while (lastErrors.Count > 25)
                    {
                        lastErrors.Dequeue();
                    }
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null || durationSeconds <= 0 || !e.Data.StartsWith("out_time_us=", StringComparison.Ordinal))
                {
                    return;
                }

                if (long.TryParse(e.Data.AsSpan("out_time_us=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out long us))
                {
                    double fraction = us / 1_000_000.0 / durationSeconds;

                    try
                    {
                        BeginInvoke(onProgress, fraction);
                    }
                    catch (InvalidOperationException)
                    {
                        // Window closing.
                    }
                }
            };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                return (-1, $"Could not start FFmpeg: {ex.Message}");
            }

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            using (token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (Exception)
                {
                    // Already exited.
                }
            }))
            {
                await process.WaitForExitAsync();
            }

            string errors;

            lock (lastErrors)
            {
                errors = string.Join(Environment.NewLine, lastErrors);
            }

            return (process.ExitCode, errors);
        }

        private static string GetOutputPath(string input, string outputFolder, bool hevc)
        {
            string folder = outputFolder.Length > 0 ? outputFolder : Path.GetDirectoryName(input);
            string name = Path.GetFileNameWithoutExtension(input) + (hevc ? "_hevc" : "_h264");
            string path = Path.Combine(folder, name + ".mp4");

            for (int i = 2; File.Exists(path) || string.Equals(path, input, StringComparison.OrdinalIgnoreCase); i++)
            {
                path = Path.Combine(folder, $"{name} ({i}).mp4");
            }

            return path;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Best effort.
            }
        }

        private static string FormatSize(long bytes)
        {
            double value = Math.Abs(bytes);
            string[] units = ["bytes", "KB", "MB", "GB", "TB"];
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{(bytes < 0 ? "-" : string.Empty)}{value:F1} {units[unit]}";
        }

        private static string FormatTime(TimeSpan time)
        {
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours} h {time.Minutes} min"
                : time.TotalMinutes >= 1
                    ? $"{(int)time.TotalMinutes} min {time.Seconds} s"
                    : $"{time.Seconds} s";
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cts == null)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(this, "A conversion is running. Stop it and close?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer == DialogResult.Yes)
            {
                _cts.Cancel();
            }
            else
            {
                e.Cancel = true;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            _list.DoubleClick += (_, _) =>
            {
                if (_list.SelectedItems.Count == 1 && _list.SelectedItems[0].SubItems[0].Tag is string details && details.Length > 0)
                {
                    MessageBox.Show(this, details, "FFmpeg output", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
        }

        /// <summary>
        /// Same search as SuperSwitch's recorder: next to the program, on PATH, then the winget link folder.
        /// </summary>
        private static string FindFfmpeg()
        {
            const string ExeName = "ffmpeg.exe";
            string baseDir = AppContext.BaseDirectory;

            string[] candidates =
            [
                Path.Combine(baseDir, ExeName),
                Path.Combine(baseDir, "ffmpeg", ExeName),
                Path.Combine(baseDir, "ffmpeg", "bin", ExeName),
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim('"'), ExeName);

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Invalid PATH entry.
                }
            }

            string links = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", ExeName);

            return File.Exists(links) ? links : null;
        }
    }
}
