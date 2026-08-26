using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace FuckLogs
{
    // ULP tab — Url:Login:Password combo extractor.
    // Mirrors the Miner tab feature set: multi-source files/folders with drag & drop,
    // custom output folder, host-keyword filter, combined/split output modes,
    // email/username credential-type filter, live progress + log, results handoff.
    class UlpTab : Panel
    {
        // Raised when a run produced results — MainForm loads them into the Results page.
        public Action<string> ResultsReady;

        private readonly ListBox sourcesList = new();
        private readonly Label sourcesOverlay = new();
        private readonly PillButton addFilesButton = new();
        private readonly PillButton addFolderButton = new();
        private readonly PillButton removeSourceButton = new();
        private readonly PillButton clearSourcesButton = new();
        private readonly FlatTextBox outputDirBox = new();
        private readonly FlatTextBox keywordsBox = new();
        private readonly FlatRadio radioCombined = new();
        private readonly FlatRadio radioSplit = new();
        private readonly FlatRadio radioKindBoth = new();
        private readonly FlatRadio radioKindEmails = new();
        private readonly FlatRadio radioKindUsernames = new();
        private readonly FlatCheck chkMatchLogin = new();
        private readonly PillButton startButton = new();
        private readonly PillButton cancelButton = new();
        private readonly PillButton outBrowseButton = new();
        private readonly PillButton openOutputButton = new();
        private readonly FlatProgressBar progressBar = new();
        private readonly Label statusLabel = new();
        private readonly TextBox logBox = new();

        private CancellationTokenSource _cts;
        private string _lastOutputRoot;
        private readonly Stopwatch runSw = new();

        public UlpTab()
        {
            BackColor = Theme.Canvas;

﻿            // ===== Sources =====
            const int CtrlH = 34;
            const int Gap = 10;
            const int LabelH = 34;
            const int DropH = 100;

            var srcLabel = MainForm.SectionLabel("SOURCES", "Drop ULP files/folders here, or use Files / Folder.");
            sourcesList.Dock = DockStyle.Fill;
            sourcesList.SelectionMode = SelectionMode.MultiExtended;
            sourcesList.Font = Theme.MonoSmall;
            sourcesList.ForeColor = Theme.Graphite;
            sourcesList.BorderStyle = BorderStyle.None;
            sourcesList.HorizontalScrollbar = true;
            sourcesList.AllowDrop = true;

            sourcesOverlay.Text = "Drop ULP files here";
            sourcesOverlay.Font = Theme.Meta;
            sourcesOverlay.ForeColor = Theme.Stone;
            sourcesOverlay.BackColor = Theme.Canvas;
            sourcesOverlay.TextAlign = ContentAlignment.MiddleCenter;
            sourcesOverlay.Dock = DockStyle.Fill;
            sourcesOverlay.Tag = "lifted";
            sourcesOverlay.AllowDrop = true;
            var srcHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Lifted, Tag = "lifted" };
            srcHost.Controls.Add(sourcesOverlay);
            srcHost.Controls.Add(sourcesList);
            var sourcesFrame = MainForm.HairlineBox(srcHost, -1);

            addFilesButton.Text = "Files";
            addFilesButton.Ghost = true;
            addFolderButton.Text = "Folder";
            addFolderButton.Ghost = true;
            removeSourceButton.Text = "Remove";
            removeSourceButton.Ghost = true;
            clearSourcesButton.Text = "Clear";
            clearSourcesButton.Ghost = true;
            var srcButtons = MainForm.EqualButtonRow(CtrlH, addFilesButton, addFolderButton, removeSourceButton, clearSourcesButton);

            var outLabel = MainForm.SectionLabel("OUTPUT FOLDER", null);
            outputDirBox.Dock = DockStyle.Fill;
            outputDirBox.Height = CtrlH;
            outputDirBox.PlaceholderText = Path.Combine(Directory.GetCurrentDirectory(), "FuckLogs_Output") + " (default)";
            outBrowseButton.Text = "Browse";
            outBrowseButton.Ghost = true;
            outBrowseButton.Dock = DockStyle.Fill;
            outBrowseButton.Height = CtrlH;
            var outRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Lifted,
                Tag = "lifted"
            };
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            outRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outBrowseButton.Margin = new Padding(8, 0, 0, 0);
            outRow.Controls.Add(outputDirBox, 0, 0);
            outRow.Controls.Add(outBrowseButton, 1, 0);

            var kwLabel = MainForm.SectionLabel("FILTER BY SITE", "Optional - e.g. facebook, grammarly. Empty keeps everything.");
            keywordsBox.Dock = DockStyle.Fill;
            keywordsBox.Height = CtrlH;
            keywordsBox.PlaceholderText = "facebook, grammarly";

            radioCombined.Text = "One folder";
            radioCombined.Checked = true;
            radioSplit.Text = "Split by keyword";
            var modeFlow = MainForm.LabeledOptionRow("Mode", CtrlH, radioCombined, radioSplit);

            radioKindBoth.Text = "Both";
            radioKindBoth.Checked = true;
            radioKindEmails.Text = "Emails";
            radioKindUsernames.Text = "Usernames";
            var extractFlow = MainForm.LabeledOptionRow("Type", CtrlH, radioKindBoth, radioKindEmails, radioKindUsernames);

            chkMatchLogin.Text = "Match keywords in login too";
            chkMatchLogin.Dock = DockStyle.Fill;
            chkMatchLogin.Margin = new Padding(0, 2, 0, 2);
            chkMatchLogin.AutoSize = false;
            chkMatchLogin.Height = 24;
            var optionsRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Lifted, Tag = "lifted", Margin = new Padding(0) };
            optionsRow.Controls.Add(chkMatchLogin);

            startButton.Text = "Extract ULPs";
            cancelButton.Text = "Cancel";
            cancelButton.Ghost = true;
            cancelButton.Enabled = false;
            openOutputButton.Text = "Open Output";
            openOutputButton.Ghost = true;
            openOutputButton.Enabled = false;
            var buttonRow = MainForm.EqualButtonRow(CtrlH, startButton, cancelButton, openOutputButton);

            progressBar.Dock = DockStyle.Top;
            progressBar.Height = 6;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Font = Theme.Meta;
            statusLabel.ForeColor = Theme.Slate;
            statusLabel.Tag = "slate";
            statusLabel.AutoEllipsis = true;
            statusLabel.Text = "Ready - add ULP files to begin.";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            var progressPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            progressPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            progressPanel.Controls.Add(progressBar, 0, 0);
            progressPanel.Controls.Add(statusLabel, 0, 1);

            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;
            logBox.WordWrap = false;
            logBox.BorderStyle = BorderStyle.None;
            logBox.Font = Theme.MonoSmall;
            logBox.BackColor = Theme.Scrim;
            logBox.ForeColor = Theme.ScrimText;
            var logHeader = new Label
            {
                Text = "ACTIVITY",
                Font = Theme.Eyebrow,
                ForeColor = Theme.Stone,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "stone",
                Padding = new Padding(2, 0, 0, 0)
            };
            var logFrame = new CardPanel { Dock = DockStyle.Fill, UseScrim = true, Radius = Theme.RStadium, Padding = new Padding(18, 14, 18, 14) };
            logFrame.Controls.Add(logBox);

            var advancedInner = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Theme.Lifted,
                Tag = "lifted",
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            advancedInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            void AdvRow(Control c, int h)
            {
                c.Margin = new Padding(0);
                advancedInner.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
                advancedInner.Controls.Add(c, 0, advancedInner.RowCount);
                advancedInner.RowCount++;
            }
            advancedInner.RowCount = 0;
            AdvRow(outLabel, 22);
            AdvRow(outRow, CtrlH);
            AdvRow(MainForm.Spacer(8), 8);
            AdvRow(modeFlow, CtrlH);
            AdvRow(extractFlow, CtrlH);
            AdvRow(MainForm.Spacer(6), 6);
            AdvRow(optionsRow, 28);

            var advancedBody = new CardPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RButton,
                Padding = new Padding(14, 12, 14, 12),
                Visible = false,
                Tag = "lifted"
            };
            advancedBody.Controls.Add(advancedInner);

            var advancedToggle = new PillButton { Ghost = true, Text = "More options >", Height = CtrlH, Width = 132 };
            advancedToggle.Dock = DockStyle.Left;
            var advancedHint = new Label
            {
                Text = "Output folder, modes, filters",
                Font = Theme.Meta,
                ForeColor = Theme.Stone,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "stone",
                Padding = new Padding(12, 0, 0, 0)
            };
            var advToggleRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Tag = "canvas", Margin = new Padding(0) };
            advToggleRow.Controls.Add(advancedHint);
            advToggleRow.Controls.Add(advancedToggle);

            var rightCol = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            rightCol.RowStyles.Add(new RowStyle(SizeType.Absolute, LabelH));
            rightCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightCol.Controls.Add(logHeader, 0, 0);
            rightCol.Controls.Add(logFrame, 0, 1);

            var leftCol = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            int leftAdvRow = -1;
            void LeftRow(Control c, int h)
            {
                c.Margin = new Padding(0);
                leftCol.RowStyles.Add(h < 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h));
                leftCol.Controls.Add(c, 0, leftCol.RowCount);
                leftCol.RowCount++;
            }
            leftCol.RowCount = 0;
            LeftRow(srcLabel, LabelH);
            LeftRow(sourcesFrame, DropH);
            LeftRow(MainForm.Spacer(Gap), Gap);
            LeftRow(srcButtons, CtrlH);
            LeftRow(MainForm.Spacer(Gap + 2), Gap + 2);
            LeftRow(kwLabel, LabelH);
            LeftRow(keywordsBox, CtrlH);
            LeftRow(MainForm.Spacer(Gap), Gap);
            LeftRow(advToggleRow, CtrlH);
            leftAdvRow = leftCol.RowCount;
            LeftRow(advancedBody, 0);
            LeftRow(MainForm.Spacer(Gap), Gap);
            LeftRow(buttonRow, CtrlH);
            LeftRow(MainForm.Spacer(6), 6);
            LeftRow(progressPanel, 32);
            LeftRow(MainForm.Spacer(1), -1);

            bool advancedOn = false;
            void SetAdvanced(bool show)
            {
                advancedOn = show;
                advancedBody.Visible = show;
                if (show)
                {
                    advancedInner.PerformLayout();
                    int innerH = advancedInner.PreferredSize.Height;
                    if (innerH < 10)
                        innerH = advancedInner.GetPreferredSize(new Size(Math.Max(120, leftCol.ClientSize.Width), 0)).Height;
                    int h = Math.Max(140, innerH + advancedBody.Padding.Vertical + 4);
                    leftCol.RowStyles[leftAdvRow].SizeType = SizeType.Absolute;
                    leftCol.RowStyles[leftAdvRow].Height = h;
                }
                else
                {
                    leftCol.RowStyles[leftAdvRow].SizeType = SizeType.Absolute;
                    leftCol.RowStyles[leftAdvRow].Height = 0;
                }
                advancedToggle.Text = show ? "More options v" : "More options >";
                advancedHint.Visible = !show;
                leftCol.PerformLayout();
            }
            advancedToggle.Click += (s, e) => SetAdvanced(!advancedOn);
            leftCol.Resize += (s, e) => { if (advancedOn) SetAdvanced(true); };
            SetAdvanced(false);

            var workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(24, 16, 24, 16),
                Margin = new Padding(0),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftCol.Margin = new Padding(0, 0, 12, 0);
            rightCol.Margin = new Padding(12, 0, 0, 0);
            workspace.Controls.Add(leftCol, 0, 0);
            workspace.Controls.Add(rightCol, 1, 0);
            Controls.Add(workspace);

            // ===== Events =====
            addFilesButton.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Multiselect = true, Filter = "ULP files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Select ULP files" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    foreach (var f in dlg.FileNames) AddSource(f);
            };
            addFolderButton.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select a folder of ULP files", UseDescriptionForTitle = true };
                if (dlg.ShowDialog(this) == DialogResult.OK) AddSource(dlg.SelectedPath);
            };
            removeSourceButton.Click += (s, e) =>
            {
                foreach (var item in sourcesList.SelectedItems.Cast<string>().ToArray())
                    sourcesList.Items.Remove(item);
                sourcesOverlay.Visible = sourcesList.Items.Count == 0;
            };
            clearSourcesButton.Click += (s, e) => { sourcesList.Items.Clear(); sourcesOverlay.Visible = true; };
            sourcesList.DragEnter += (s, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
            };
            sourcesList.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                    foreach (var p in paths) AddSource(p);
            };
            sourcesOverlay.DragEnter += (s, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
            };
            sourcesOverlay.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                    foreach (var p in paths) AddSource(p);
            };
            sourcesOverlay.Click += (s, e) => addFilesButton.PerformClick();
            outBrowseButton.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select output folder", UseDescriptionForTitle = true };
                string current = outputDirBox.Text.Trim();
                if (current.Length > 0 && Directory.Exists(current)) dlg.InitialDirectory = current;
                if (dlg.ShowDialog(this) == DialogResult.OK) outputDirBox.Text = dlg.SelectedPath;
            };
            startButton.Click += StartButton_Click;
            cancelButton.Click += (s, e) => _cts?.Cancel();
            openOutputButton.Click += (s, e) =>
            {
                if (_lastOutputRoot != null && Directory.Exists(_lastOutputRoot))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_lastOutputRoot}\"") { UseShellExecute = true });
            };

            // First-run how-to in the log console
            Log("ULP mode — extracts url:login:password lines from .txt files.");
            Log("Example line: https://site.com/login:user@mail.com:pass123");
            Log("1. Drop ULP files on the left, or click Files / Folder.");
            Log("  2. Optional: type site keywords to filter, e.g. facebook, grammarly.");
            Log("  3. Press Extract ULPs — results appear in the Results tab.");
            Log("Tip: 'Advanced options' holds the output folder, modes & extra filters.");
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            var sources = sourcesList.Items.Cast<string>().ToArray();
            if (sources.Length == 0)
            {
                MessageBox.Show(this, "Add at least one ULP file or folder first.", "FuckLogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outDir = outputDirBox.Text.Trim();
            if (outDir.Length > 0 && !Directory.Exists(outDir))
            {
                try { Directory.CreateDirectory(outDir); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Cannot create output folder:\n{ex.Message}", "FuckLogs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            string[] keywords = keywordsBox.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var opt = new UlpOptions
            {
                Sources = sources,
                Keywords = keywords,
                SplitByKeyword = radioSplit.Checked,
                OutputDir = outDir,
                MatchLogin = chkMatchLogin.Checked,
                ExtractEmails = radioKindBoth.Checked || radioKindEmails.Checked,
                ExtractUsernames = radioKindBoth.Checked || radioKindUsernames.Checked
            };
            bool effectiveSplit = opt.SplitByKeyword && keywords.Length > 1;

            SetRunning(true);
            logBox.Clear();
            openOutputButton.Enabled = false;
            _lastOutputRoot = null;
            runSw.Restart();
            Log($"Sources : {sources.Length}");
            foreach (var s in sources) Log($"  - {s}");
            Log($"Keywords: {(keywords.Length == 0 ? "ALL" : string.Join(", ", keywords))}   |   Mode: {(effectiveSplit ? "Separate per keyword" : "Combined")}   |   Match: {(opt.MatchLogin ? "Host+Login" : "Host")}");

            Log("");

            _cts = new CancellationTokenSource();
            var progress = new Progress<MinerProgress>(OnProgress);

            try
            {
                var result = await UlpEngine.RunAsync(opt, progress, _cts.Token);
                runSw.Stop();
                RenderResult(result);
                if (!result.Cancelled && result.Outputs.Count > 0 && result.Creds > 0)
                {
                    _lastOutputRoot = result.TopLevelOutput;
                    openOutputButton.Enabled = true;
                    ResultsReady?.Invoke(result.TopLevelOutput);
                }
            }
            catch (Exception ex)
            {
                runSw.Stop();
                Log("ERROR: " + ex.Message);
            }
            finally
            {
                SetRunning(false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnProgress(MinerProgress p)
        {
            int pct = p.FilesTotal > 0 ? (int)((long)p.FilesDone * 100 / p.FilesTotal) : 0;
            progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, pct));
            double perSec = p.FilesDone / Math.Max(0.1, runSw.Elapsed.TotalSeconds);
            statusLabel.Text = $"{p.FilesDone:N0} / {p.FilesTotal:N0} files ({pct}%)  ·  {p.Creds:N0} ULPs  ·  {perSec:N0} files/s  ·  {p.CurrentFile}";
        }

        private void RenderResult(MinerResult r)
        {
            Log("");
            Log("Summary");
            Log($"Files processed : {r.Files:N0}");
            Log($"Lines read      : {r.Lines:N0}");
            Log($"ULP combos      : {r.Creds:N0}");
            Log($"Filtered out    : {r.Filtered:N0}");
            if (r.SplitByKeyword)
            {
                foreach (var o in r.Outputs)
                    Log($"  [{o.Label}]  emails: {o.Emails:N0}   usernames: {o.Usernames:N0}   hosts: {o.Hosts:N0}");
            }
            else if (r.Outputs.Count > 0)
            {
                var o = r.Outputs[0];
                Log($"Email logins    : {o.Emails:N0}");
                Log($"Username logins : {o.Usernames:N0}");
                Log($"Unique hosts    : {o.Hosts:N0}");
                if (o.TopHosts.Count > 0)
                {
                    Log("Top hosts:");
                    foreach (var h in o.TopHosts) Log($"   {h.Key} — {h.Value:N0}");
                }
            }
            Log($"Output size     : {r.OutputBytes:N0} bytes");
            Log($"Elapsed         : {r.Elapsed.TotalSeconds:0.0}s");
            foreach (var n in r.Notes) Log("· " + n);
            if (r.Cancelled) Log("Cancelled.");
            else if (r.Outputs.Count > 0) Log($"Done. Output: {r.TopLevelOutput}");
        }

        private void Log(string text)
        {
            logBox.AppendText(text + Environment.NewLine);
        }

        private void AddSource(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show(this, $"Not found (skipped):\n{path}", "FuckLogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            foreach (var existing in sourcesList.Items.Cast<string>())
                if (string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)) return;
            sourcesList.Items.Add(path);
            sourcesOverlay.Visible = false;
        }

        private void SetRunning(bool running)
        {
            startButton.Enabled = !running;
            cancelButton.Enabled = running;
            sourcesList.Enabled = !running;
            addFilesButton.Enabled = addFolderButton.Enabled = !running;
            removeSourceButton.Enabled = clearSourcesButton.Enabled = !running;
            outputDirBox.Enabled = !running;
            outBrowseButton.Enabled = !running;
            keywordsBox.Enabled = !running;
            radioCombined.Enabled = radioSplit.Enabled = !running;
            radioKindBoth.Enabled = radioKindEmails.Enabled = radioKindUsernames.Enabled = !running;
            chkMatchLogin.Enabled = !running;
            if (running)
            {
                progressBar.Value = 0;
                statusLabel.Text = "Scanning...";
            }
        }
    }
}

