using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace FuckLogs
{
    class MainForm : Form
    {
        // Sources / miner tab controls

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

        private readonly FlatCheck chkIncludePassOnly = new();

        private readonly FlatCheck chkPasswordsOnly = new();

        private readonly FlatCheck chkTopMost = new();

        private readonly PillButton startButton = new();

        private readonly PillButton cancelButton = new();

        private readonly PillButton outBrowseButton = new();

        private readonly PillButton openOutputButton = new();

        private readonly FlatProgressBar progressBar = new();

        private readonly Label statusLabel = new();

        private readonly TextBox logBox = new();
        // Results tab controls

        private readonly FlatTextBox filterBox = new();

        private readonly ListBox resultsList = new();

        private readonly PillButton copyButton = new();

        private readonly Label resultCountLabel = new();
        // Segmented stadium nav (icon + label + active top bar)

        private readonly NavSegment navSegMiner = new();

        private readonly NavSegment navSegUlp = new();

        private readonly NavSegment navSegResults = new();

        private readonly Panel pageMiner = new();

        private readonly UlpTab pageUlp = new();

        private readonly Panel pageResults = new();

        private readonly FlatCheck chkDarkMode = new();

        private readonly System.Windows.Forms.Timer filterTimer = new();

        private readonly Stopwatch runSw = new();

        private CancellationTokenSource _cts;

        private string _lastOutputRoot;

        private string[] _allResults = Array.Empty<string>();

        private string[] _filteredResults = Array.Empty<string>();

        public MainForm()
        {
            Theme.IsDark = LoadDarkSetting();
            AutoScaleMode = AutoScaleMode.None;
            Text = $"FuckLogs v{Program.AppVersion} — Credential Miner";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 740);
            MinimumSize = new Size(960, 620);
            Font = Theme.Body;
            BackColor = Theme.Canvas;
            // ===== Compact chrome: wordmark + nav + dark mode in one bar =====
            var chrome = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Canvas, Padding = new Padding(24, 10, 24, 10), Tag = "canvas" };
            chrome.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Hairline, 1f);
                e.Graphics.DrawLine(pen, 24, chrome.Height - 1, chrome.Width - 24, chrome.Height - 1);
            };
            var brandCol = new Panel { Dock = DockStyle.Left, Width = 150, BackColor = Theme.Canvas, Tag = "canvas" };
            var wordmark = new Label
            {
                Text = "FuckLogs",
                Font = Theme.Display,
                ForeColor = Theme.Ink,
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.BottomLeft,
                Tag = "ink"
            };
            var versionLabel = new Label
            {
                Text = $"v{Program.AppVersion}",
                Font = Theme.Meta,
                ForeColor = Theme.Stone,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Tag = "stone"
            };
            brandCol.Controls.Add(versionLabel);
            brandCol.Controls.Add(wordmark);
            // Segmented stadium nav - icons, active blue text, top accent bar
            var navPill = new CardPanel
            {
                Radius = Theme.RPill,
                UseSurface = true,
                Shadow = true,
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                Tag = "surface",
                Padding = new Padding(8, 4, 8, 4), // keeps the active segment pill clear of the stadium caps
            };
            navSegMiner.Text = "Stealer Logs";
            navSegMiner.IconKind = NavIconKind.Document;
            navSegMiner.SegmentPosition = NavSegmentPosition.Start;
            navSegUlp.Text = "ULP Combos";
            navSegUlp.IconKind = NavIconKind.Layers;
            navSegUlp.SegmentPosition = NavSegmentPosition.Middle;
            navSegResults.Text = "Results";
            navSegResults.IconKind = NavIconKind.Trophy;
            navSegResults.SegmentPosition = NavSegmentPosition.End;
            foreach (var seg in new[] { navSegMiner, navSegUlp, navSegResults })
            {
                seg.Dock = DockStyle.Fill;
                seg.Tag = "nav";
                seg.Margin = new Padding(0);
            }
            var navSegs = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Tag = "surface",
                Margin = new Padding(0),
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            navSegs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            navSegs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            navSegs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            navSegs.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            navSegs.Controls.Add(navSegMiner, 0, 0);
            navSegs.Controls.Add(navSegUlp, 1, 0);
            navSegs.Controls.Add(navSegResults, 2, 0);
            navPill.Controls.Add(navSegs);
            navSegMiner.Click += (s, e) => ShowPage(0);
            navSegUlp.Click += (s, e) => ShowPage(1);
            navSegResults.Click += (s, e) => ShowPage(2);
            // Center the pill between brand and dark toggle
            var navWrap = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(16, 2, 16, 2), Tag = "canvas" };
            var navCenter = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Tag = "canvas" };
            // Wider pill so icon + label fit cleanly
            var navHolder = new Panel { Width = 480, Height = 40, BackColor = Theme.Canvas, Tag = "canvas" };
            navPill.Dock = DockStyle.Fill;
            navHolder.Controls.Add(navPill);
            void CenterNav()
            {
                navHolder.Left = Math.Max(0, (navCenter.ClientSize.Width - navHolder.Width) / 2);
                navHolder.Top = Math.Max(0, (navCenter.ClientSize.Height - navHolder.Height) / 2);
            }
            navCenter.Resize += (s, e) => CenterNav();
            navCenter.Controls.Add(navHolder);
            navWrap.Controls.Add(navCenter);
            chkDarkMode.Text = "Dark";
            chkDarkMode.BackColor = Color.Transparent;
            chkDarkMode.Dock = DockStyle.Right;
            chkDarkMode.Width = 72;
            chkDarkMode.CheckedChanged += (s, e) => SetDarkMode(chkDarkMode.Checked);
            chrome.Controls.Add(navWrap);   // fill center first
            chrome.Controls.Add(chkDarkMode);
            chrome.Controls.Add(brandCol);
            CenterNav();
            // ===== Pages container =====
            var pagesHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Tag = "canvas" };
            pageMiner.Dock = DockStyle.Fill;
            pageMiner.BackColor = Theme.Canvas;
            pageMiner.Tag = "canvas";
            pageUlp.Dock = DockStyle.Fill;
            pageUlp.BackColor = Theme.Canvas;
            pageUlp.Visible = false;
            pageUlp.ResultsReady = root => LoadResults(root);
            pageResults.Dock = DockStyle.Fill;
            pageResults.BackColor = Theme.Canvas;
            pageResults.Visible = false;
            pageResults.Tag = "canvas";
            pagesHost.Controls.Add(pageResults);
            pagesHost.Controls.Add(pageUlp);
            pagesHost.Controls.Add(pageMiner);
            // ===== Miner: sources list =====
            // Shared metrics so every control shares the same left edge, height and gap.
            const int CtrlH = 34;
            const int Gap = 10;
            const int LabelH = 34;
            const int DropH = 100;
            var srcLabel = SectionLabel("SOURCES", "Drop files/folders here, or use Files / Folder.");
            sourcesList.Dock = DockStyle.Fill;
            sourcesList.SelectionMode = SelectionMode.MultiExtended;
            sourcesList.Font = Theme.MonoSmall;
            sourcesList.ForeColor = Theme.Graphite;
            sourcesList.BorderStyle = BorderStyle.None;
            sourcesList.HorizontalScrollbar = true;
            sourcesList.AllowDrop = true;
            sourcesOverlay.Text = "Drop log files here";
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
            var sourcesFrame = HairlineBox(srcHost, -1);
            addFilesButton.Text = "Files";
            addFilesButton.Ghost = true;
            addFolderButton.Text = "Folder";
            addFolderButton.Ghost = true;
            removeSourceButton.Text = "Remove";
            removeSourceButton.Ghost = true;
            clearSourcesButton.Text = "Clear";
            clearSourcesButton.Ghost = true;
            var srcButtons = EqualButtonRow(CtrlH, addFilesButton, addFolderButton, removeSourceButton, clearSourcesButton);
            var outLabel = SectionLabel("OUTPUT FOLDER", null);
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
            var kwLabel = SectionLabel("FILTER BY SITE", "Optional - e.g. freepik, facebook. Empty keeps everything.");
            keywordsBox.Dock = DockStyle.Fill;
            keywordsBox.Height = CtrlH;
            keywordsBox.PlaceholderText = "freepik, magnific";
            radioCombined.Text = "One folder";
            radioCombined.Checked = true;
            radioSplit.Text = "Split by keyword";
            var modeTip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300 };
            modeTip.SetToolTip(radioCombined, "All matching credentials go into ONE output folder.");
            modeTip.SetToolTip(radioSplit, "Each keyword gets its own folder. Only matters with 2+ keywords.");
            var modeFlow = LabeledOptionRow("Mode", CtrlH, radioCombined, radioSplit);
            radioKindBoth.Text = "Both";
            radioKindBoth.Checked = true;
            radioKindEmails.Text = "Emails";
            radioKindUsernames.Text = "Usernames";
            var extractFlow = LabeledOptionRow("Type", CtrlH, radioKindBoth, radioKindEmails, radioKindUsernames);
            chkMatchLogin.Text = "Match keywords in login too";
            chkIncludePassOnly.Text = "Keep password-only rows";
            chkIncludePassOnly.Checked = true;
            chkPasswordsOnly.Text = "Only scan password lists (faster)";
            chkPasswordsOnly.Checked = true;
            chkTopMost.Text = "Always on top";
            var optTip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300 };
            optTip.SetToolTip(chkPasswordsOnly, "Folder scans only pick up .txt files with 'password' or 'login' in the name\n(passwords.txt, All_Passwords.txt, logins.txt, ...). Files you add explicitly are always scanned.\nTurn off to scan every .txt file (slower on big folders).");
            var optionsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Lifted,
                Tag = "lifted"
            };
            optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            optionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            optionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            foreach (var cb in new FlatCheck[] { chkMatchLogin, chkIncludePassOnly, chkPasswordsOnly, chkTopMost })
            {
                cb.Dock = DockStyle.Fill;
                cb.Margin = new Padding(0, 2, 8, 2);
                cb.AutoSize = false;
                cb.Height = 24;
            }
            optionsRow.Controls.Add(chkMatchLogin, 0, 0);
            optionsRow.Controls.Add(chkIncludePassOnly, 1, 0);
            optionsRow.Controls.Add(chkPasswordsOnly, 0, 1);
            optionsRow.Controls.Add(chkTopMost, 1, 1);
            startButton.Text = "Start Mining";
            cancelButton.Text = "Cancel";
            cancelButton.Ghost = true;
            cancelButton.Enabled = false;
            openOutputButton.Text = "Open Output";
            openOutputButton.Ghost = true;
            openOutputButton.Enabled = false;
            var buttonRow = EqualButtonRow(CtrlH, startButton, cancelButton, openOutputButton);
            progressBar.Dock = DockStyle.Top;
            progressBar.Height = 6;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Font = Theme.Meta;
            statusLabel.ForeColor = Theme.Slate;
            statusLabel.Tag = "slate";
            statusLabel.AutoEllipsis = true;
            statusLabel.Text = "Ready - add log files to begin.";
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
            AdvRow(Spacer(8), 8);
            AdvRow(modeFlow, CtrlH);
            AdvRow(extractFlow, CtrlH);
            AdvRow(Spacer(6), 6);
            AdvRow(optionsRow, 52);
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
            LeftRow(Spacer(Gap), Gap);
            LeftRow(srcButtons, CtrlH);
            LeftRow(Spacer(Gap + 2), Gap + 2);
            LeftRow(kwLabel, LabelH);
            LeftRow(keywordsBox, CtrlH);
            LeftRow(Spacer(Gap), Gap);
            LeftRow(advToggleRow, CtrlH);
            leftAdvRow = leftCol.RowCount;
            LeftRow(advancedBody, 0);
            LeftRow(Spacer(Gap), Gap);
            LeftRow(buttonRow, CtrlH);
            LeftRow(Spacer(6), 6);
            LeftRow(progressPanel, 32);
            LeftRow(Spacer(1), -1);
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
                    int h = Math.Max(150, innerH + advancedBody.Padding.Vertical + 4);
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
            var minerWorkspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(24, 16, 24, 16),
                Margin = new Padding(0),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            minerWorkspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            minerWorkspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            minerWorkspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftCol.Margin = new Padding(0, 0, 12, 0);
            rightCol.Margin = new Padding(12, 0, 0, 0);
            minerWorkspace.Controls.Add(leftCol, 0, 0);
            minerWorkspace.Controls.Add(rightCol, 1, 0);
            pageMiner.Controls.Add(minerWorkspace);
            // ===== Results page =====
            var resLabel = SectionLabel("EXTRACTED CREDENTIALS", "Filter, double-click a row to copy, or Copy All.");
            filterBox.Dock = DockStyle.Fill;
            filterBox.PlaceholderText = "Type to filter...";
            copyButton.Text = "Copy All";
            copyButton.Ghost = true;
            copyButton.Width = 100;
            var filterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0), BackColor = Theme.Canvas, Tag = "canvas" };
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filterRow.Controls.Add(filterBox, 0, 0);
            filterRow.Controls.Add(copyButton, 1, 0);
            resultCountLabel.Text = "Nothing here yet — run a scan from Stealer Logs or ULP Combos.";
            resultCountLabel.Dock = DockStyle.Fill;
            resultCountLabel.Font = Theme.Meta;
            resultCountLabel.ForeColor = Theme.Stone;
            resultCountLabel.Tag = "stone";
            resultCountLabel.TextAlign = ContentAlignment.MiddleLeft;
            resultsList.Dock = DockStyle.Fill;
            resultsList.Font = Theme.MonoSmall;
            resultsList.ForeColor = Theme.Ink;
            resultsList.BorderStyle = BorderStyle.None;
            resultsList.HorizontalScrollbar = true;
            var resultsFrame = HairlineBox(resultsList, -1);
            var resultsStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(24, 16, 24, 16),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            resultsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            resultsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            resultsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            resultsStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            resultsStack.Controls.Add(resLabel, 0, 0);
            resultsStack.Controls.Add(filterRow, 0, 1);
            resultsStack.Controls.Add(resultCountLabel, 0, 2);
            resultsStack.Controls.Add(resultsFrame, 0, 3);
            pageResults.Controls.Add(resultsStack);
            Controls.Add(pagesHost);
            Controls.Add(chrome);
            // First-run how-to in the log console
            Log($"Welcome to FuckLogs v{Program.AppVersion}");
            Log("1. Drop log files on the left, or click Files / Folder.");
            Log("2. Optional: filter by site keywords.");
            Log("3. Press Start Mining — combos land in Results.");
            Log("Tip: More options holds output folder, modes & extra filters.");
            ShowPage(0);
            // Apply saved dark mode after the whole tree exists (walks + recolors controls)
            if (Theme.IsDark) chkDarkMode.Checked = true;
            // ===== Events =====
            addFilesButton.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Multiselect = true, Filter = "Log files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Select log files" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    foreach (var f in dlg.FileNames) AddSource(f);
            };
            addFolderButton.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select a folder of logs", UseDescriptionForTitle = true };
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
            chkTopMost.CheckedChanged += (s, e) => TopMost = chkTopMost.Checked;
            filterTimer.Interval = 300;
            filterTimer.Tick += (s, e) => { filterTimer.Stop(); ApplyFilter(); };
            filterBox.Inner.TextChanged += (s, e) => { filterTimer.Stop(); filterTimer.Start(); };
            copyButton.Click += (s, e) =>
            {
                if (_filteredResults.Length == 0) return;
                Clipboard.SetText(string.Join(Environment.NewLine, _filteredResults));
                resultCountLabel.Text = $"Copied {_filteredResults.Length:N0} combos to clipboard.";
            };
            resultsList.DoubleClick += (s, e) =>
            {
                if (resultsList.SelectedItem is string sel)
                {
                    Clipboard.SetText(sel);
                    resultCountLabel.Text = "Copied: " + sel;
                }
            };
        }
        // Equal-width buttons sharing one row height - keeps edges flush.

        internal static TableLayoutPanel EqualButtonRow(int height, params PillButton[] buttons)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = buttons.Length,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Canvas,
                Tag = "canvas"
            };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < buttons.Length; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.Length));
                var b = buttons[i];
                b.Dock = DockStyle.Fill;
                b.Height = height;
                b.Margin = new Padding(i == 0 ? 0 : 6, 0, 0, 0);
                row.Controls.Add(b, i, 0);
            }
            return row;
        }
        // Caption + radio cluster on one baseline.

        internal static TableLayoutPanel LabeledOptionRow(string caption, int height, params FlatRadio[] radios)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Lifted,
                Tag = "lifted"
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var cap = new Label
            {
                Text = caption,
                Font = Theme.Meta,
                ForeColor = Theme.Stone,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "stone",
                Margin = new Padding(0)
            };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Theme.Lifted,
                Tag = "lifted"
            };
            foreach (var rb in radios)
            {
                rb.AutoSize = true;
                rb.Margin = new Padding(0, 0, 14, 0);
                flow.Controls.Add(rb);
            }
            row.Controls.Add(cap, 0, 0);
            row.Controls.Add(flow, 1, 0);
            return row;
        }

        internal static Panel Spacer(int h) => new() { Dock = DockStyle.Fill, Height = h, BackColor = Theme.Canvas, Tag = "canvas" };
        // Makes a stacked page adapt to the window size: the stack fills the page when
        // there's room (percent rows like the log/list stretch), and switches to
        // top-docked natural height so the page's AutoScroll shows scrollbars when
        // the window is too short — nothing ever gets silently clipped.

        internal static void MakeResponsive(TableLayoutPanel stack, ScrollableControl page)
        {
            stack.AutoSize = true;
            page.AutoScroll = true;
            void Fit()
            {
                int width = Math.Max(100, page.ClientSize.Width);
                int pref = stack.GetPreferredSize(new Size(width, 0)).Height;
                var want = pref > page.ClientSize.Height ? DockStyle.Top : DockStyle.Fill;
                if (stack.Dock != want) stack.Dock = want;
            }
            page.Resize += (s, e) => Fit();
            page.VisibleChanged += (s, e) => Fit();
            page.HandleCreated += (s, e) => Fit();
            stack.Layout += (s, e) => Fit(); // re-fit when inner sections expand/collapse
            Fit();
        }

        internal static Label SectionLabel(string eyebrow, string sub)
        {
            var lbl = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft
            };
            lbl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Color bg = lbl.Parent != null ? lbl.Parent.BackColor : Theme.Canvas;
                g.Clear(bg);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Identity: tiny signal-orange accent dot before the uppercase eyebrow
                using (var dot = new SolidBrush(Theme.Signal))
                    g.FillEllipse(dot, 1, 5, 5, 5);
                TextRenderer.DrawText(g, eyebrow.ToUpperInvariant(), Theme.Eyebrow, new Rectangle(12, 0, lbl.Width - 12, 16), Theme.Slate,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                if (sub != null)
                    TextRenderer.DrawText(g, sub, Theme.Meta, new Rectangle(1, 16, lbl.Width - 1, 18), Theme.Slate,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            return lbl;
        }

        internal static Panel Divider()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Hairline, 1f);
                e.Graphics.DrawLine(pen, 0, 12, p.Width, 12);
            };
            return p;
        }

        internal static Panel HairlineBox(Control inner, int height)
        {
            // Lifted-cream card (paper-on-paper) with 20px radius per the radius scale
            var card = new CardPanel { Dock = DockStyle.Fill, Radius = Theme.RButton, Padding = new Padding(10) };
            if (height > 0) card.Height = height;
            inner.Dock = DockStyle.Fill;
            card.Controls.Add(inner);
            return card;
        }
        // Repaint nav segments so Active / palette refresh.

        private void UpdateNavColors()
        {
            navSegMiner.Invalidate();
            navSegUlp.Invalidate();
            navSegResults.Invalidate();
        }

        private void ShowPage(int index)
        {
            pageMiner.Visible = index == 0;
            pageUlp.Visible = index == 1;
            pageResults.Visible = index == 2;
            navSegMiner.Active = index == 0;
            navSegUlp.Active = index == 1;
            navSegResults.Active = index == 2;
            UpdateNavColors();
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            var sources = sourcesList.Items.Cast<string>().ToArray();
            if (sources.Length == 0)
            {
                MessageBox.Show(this, "Add at least one log file or folder first.", "FuckLogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var opt = new MinerOptions
            {
                Sources = sources,
                Keywords = keywords,
                SplitByKeyword = radioSplit.Checked,
                OutputDir = outDir,
                MatchLogin = chkMatchLogin.Checked,
                IncludePasswordOnly = chkIncludePassOnly.Checked,
                PasswordsOnly = chkPasswordsOnly.Checked,
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
                var result = await MinerEngine.RunAsync(opt, progress, _cts.Token);
                runSw.Stop();
                RenderResult(result);
                if (!result.Cancelled && result.Outputs.Count > 0 && result.Creds > 0)
                {
                    _lastOutputRoot = result.TopLevelOutput;
                    openOutputButton.Enabled = true;
                    LoadResults(result.TopLevelOutput);
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
            statusLabel.Text = $"{p.FilesDone:N0} / {p.FilesTotal:N0} files ({pct}%)  ·  {p.Creds:N0} creds  ·  {perSec:N0} files/s  ·  {p.CurrentFile}";
        }

        private void RenderResult(MinerResult r)
        {
            Log("");
            Log("Summary");
            Log($"Files processed : {r.Files:N0}");
            Log($"Lines read      : {r.Lines:N0}");
            Log($"Credentials     : {r.Creds:N0}");
            Log($"Filtered out    : {r.Filtered:N0}");
            if (r.SplitByKeyword)
            {
                foreach (var o in r.Outputs)
                    Log($"  [{o.Label}]  emails: {o.Emails:N0}   usernames: {o.Usernames:N0}   hosts: {o.Hosts:N0}");
            }
            else if (r.Outputs.Count > 0)
            {
                var o = r.Outputs[0];
                Log($"Unique emails   : {o.Emails:N0}");
                Log($"Unique usernames: {o.Usernames:N0}");
                Log($"Unique hosts    : {o.Hosts:N0}");
                Log($"Unique software : {o.Software:N0}");
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

        private void LoadResults(string root)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Only the combo folders — by_host/by_soft splits (which may hold full
                // url:login:password ULP lines) must not leak into the results viewer.
                foreach (var f in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories))
                {
                    string dirName = Path.GetFileName(Path.GetDirectoryName(f) ?? string.Empty);
                    if (!string.Equals(dirName, "emails", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(dirName, "usernames", StringComparison.OrdinalIgnoreCase))
                        continue;
                    foreach (var line in File.ReadLines(f))
                    {
                        string t = line.Trim();
                        if (t.Length > 0) set.Add(t);
                    }
                }
            }
            catch { }
            _allResults = set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            filterBox.Text = string.Empty;
            ApplyFilter();
            if (_allResults.Length > 0)
            {
                Log($"Results viewer: {_allResults.Length:N0} unique combos loaded.");
                BeginInvoke(new Action(() => ShowPage(2)));
            }
        }

        private void ApplyFilter()
        {
            string f = filterBox.Text.Trim();
            _filteredResults = f.Length == 0
                ? _allResults
                : _allResults.Where(s => s.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            const int DisplayCap = 20000;
            resultsList.BeginUpdate();
            resultsList.Items.Clear();
            var view = _filteredResults.Length > DisplayCap ? _filteredResults.Take(DisplayCap) : _filteredResults;
            foreach (var s in view) resultsList.Items.Add(s);
            resultsList.EndUpdate();
            resultCountLabel.Text = $"{_filteredResults.Length:N0} combos" +
                (f.Length > 0 ? $" matching '{f}'" : "") +
                (_filteredResults.Length > DisplayCap ? $"  (showing first {DisplayCap:N0})" : "");
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

        private void Log(string text)
        {
            logBox.AppendText(text + Environment.NewLine);
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
            chkMatchLogin.Enabled = chkIncludePassOnly.Enabled = chkPasswordsOnly.Enabled = chkTopMost.Enabled = !running;
            if (running)
            {
                progressBar.Value = 0;
                statusLabel.Text = "Scanning...";
            }
        }

        private static string SettingsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FuckLogs");

        private static string SettingsFile => Path.Combine(SettingsDir, "settings.txt");

        private static bool LoadDarkSetting()
        {
            try { return File.ReadAllText(SettingsFile).Contains("dark=1"); }
            catch { return false; }
        }

        private void SetDarkMode(bool dark)
        {
            Theme.IsDark = dark;
            BackColor = Theme.Canvas;
            ApplyThemeColors(this);
            UpdateNavColors();
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(SettingsFile, dark ? "dark=1" : "dark=0");
            }
            catch { }
            Invalidate(true);
        }
        // Re-apply the active palette to controls that cache colors as properties.
        // Owner-drawn controls (PillButton, FlatRadio, FlatCheck, FlatProgressBar,
        // SectionLabel, Divider, nav underlines) read Theme.* at paint time and only
        // need the Invalidate below. Role hints travel in Control.Tag:
        //   "ink" | "slate" | "stone" (labels)  ·  "hairline" | "hairlineSoft" | "scrim" (panels)

        private static void ApplyThemeColors(Control c)
        {
            string role = c.Tag as string;
            switch (c)
            {
                case FlatTextBox ftb:
                    ftb.BackColor = Theme.Canvas;
                    ftb.Inner.BackColor = Theme.Surface;
                    ftb.Inner.ForeColor = Theme.Ink;
                    break;
                case FlatProgressBar fp:
                    fp.BackColor = Theme.Canvas;
                    break;
                case PillButton pb:
                    pb.BackColor = Theme.Canvas;
                    pb.ForeColor = pb.Ghost ? Theme.Ink : Theme.OnPrimary;
                    break;
                case FlatRadio or FlatCheck:
                    c.BackColor = Theme.Canvas;
                    c.ForeColor = Theme.Ink;
                    break;
                case TextBox tb when tb.Parent is FlatTextBox:
                    break; // inner field — colored by the FlatTextBox case above
                case TextBox tb: // the log console — always on the dark scrim
                    tb.BackColor = Theme.Scrim;
                    tb.ForeColor = Theme.ScrimText;
                    break;
                case ListBox lb:
                    lb.BackColor = Theme.Lifted;
                    lb.ForeColor = Theme.Graphite;
                    break;
                case Label lbl:
                    if (role == "nav") { lbl.BackColor = Color.Transparent; break; } // managed by UpdateNavColors
                    lbl.BackColor = role == "lifted" ? Theme.Lifted : Theme.Canvas;
                    lbl.ForeColor = role switch { "slate" => Theme.Slate, "stone" => Theme.Stone, _ => Theme.Ink };
                    break;
                case Panel p:
                    p.BackColor = role switch
                    {
                        "hairline" => Theme.Hairline,
                        "hairlineSoft" => Theme.HairlineSoft,
                        "scrim" => Theme.Scrim,
                        "lifted" => Theme.Lifted,
                        "surface" => Theme.Surface,
                        _ => Theme.Canvas
                    };
                    break;
                default:
                    c.BackColor = Theme.Canvas;
                    c.ForeColor = Theme.Ink;
                    break;
            }
            foreach (Control child in c.Controls) ApplyThemeColors(child);
            c.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_cts != null)
            {
                var answer = MessageBox.Show(this, "A scan is running. Cancel it and exit?", "FuckLogs",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                _cts.Cancel();
            }
            base.OnFormClosing(e);
        }
    }
}
