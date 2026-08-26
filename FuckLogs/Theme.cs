using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace FuckLogs
{
    // Design tokens from DESIGN-mastercard.md — warm editorial "putty & signal orange" system.
    // Light mode = cream canvas; dark mode = the ink footer palette inverted.
    static class Theme
    {
        public static bool IsDark;
        static class Light
        {
            public static readonly Color Canvas = ColorTranslator.FromHtml("#F3F0EE"); // warm putty canvas
            public static readonly Color Lifted = ColorTranslator.FromHtml("#FCFBFA"); // paper-on-paper cards
            public static readonly Color Surface = ColorTranslator.FromHtml("#FFFFFF"); // nav pill / inputs
            public static readonly Color Primary = ColorTranslator.FromHtml("#141413"); // ink pill CTA
            public static readonly Color OnPrimary = ColorTranslator.FromHtml("#F3F0EE"); // cream text on ink
            public static readonly Color Ink = ColorTranslator.FromHtml("#141413");
            public static readonly Color InkSoft = ColorTranslator.FromHtml("#262627");
            public static readonly Color Graphite = ColorTranslator.FromHtml("#565656");
            public static readonly Color Slate = ColorTranslator.FromHtml("#696969");
            public static readonly Color Stone = ColorTranslator.FromHtml("#8A8784");
            public static readonly Color Ash = ColorTranslator.FromHtml("#D1CDC7"); // whisper / disabled
            public static readonly Color Hairline = ColorTranslator.FromHtml("#E6E1DB");
            public static readonly Color HairlineSoft = ColorTranslator.FromHtml("#D1CDC7");
            public static readonly Color Border = ColorTranslator.FromHtml("#8A8784"); // ink @ ~50%
            public static readonly Color Signal = ColorTranslator.FromHtml("#CF4500"); // eyebrow dot
            public static readonly Color SignalLight = ColorTranslator.FromHtml("#F37338"); // arcs / progress
            public static readonly Color Accent = ColorTranslator.FromHtml("#3B82F6"); // nav active
            public static readonly Color Scrim = ColorTranslator.FromHtml("#141413"); // stadium log console
            public static readonly Color ScrimText = ColorTranslator.FromHtml("#F3F0EE");
            public static readonly Color Ghost = ColorTranslator.FromHtml("#E8E2DA"); // watermark headline
        }
        static class Dark
        {
            public static readonly Color Canvas = ColorTranslator.FromHtml("#141413");
            public static readonly Color Lifted = ColorTranslator.FromHtml("#1D1C1B");
            public static readonly Color Surface = ColorTranslator.FromHtml("#262627");
            public static readonly Color Primary = ColorTranslator.FromHtml("#F3F0EE"); // inverted pill
            public static readonly Color OnPrimary = ColorTranslator.FromHtml("#141413");
            public static readonly Color Ink = ColorTranslator.FromHtml("#F3F0EE");
            public static readonly Color InkSoft = ColorTranslator.FromHtml("#E8E2DA");
            public static readonly Color Graphite = ColorTranslator.FromHtml("#C9C5BF");
            public static readonly Color Slate = ColorTranslator.FromHtml("#A6A29B");
            public static readonly Color Stone = ColorTranslator.FromHtml("#8A8784");
            public static readonly Color Ash = ColorTranslator.FromHtml("#55534F");
            public static readonly Color Hairline = ColorTranslator.FromHtml("#2A2926");
            public static readonly Color HairlineSoft = ColorTranslator.FromHtml("#3A3835");
            public static readonly Color Border = ColorTranslator.FromHtml("#6E6B67");
            public static readonly Color Signal = ColorTranslator.FromHtml("#F37338");
            public static readonly Color SignalLight = ColorTranslator.FromHtml("#F37338");
            public static readonly Color Accent = ColorTranslator.FromHtml("#60A5FA"); // nav active
            public static readonly Color Scrim = ColorTranslator.FromHtml("#0B0B0A");
            public static readonly Color ScrimText = ColorTranslator.FromHtml("#E8E2DA");
            public static readonly Color Ghost = ColorTranslator.FromHtml("#1D1C1B");
        }
        public static Color Canvas => IsDark ? Dark.Canvas : Light.Canvas;
        public static Color Lifted => IsDark ? Dark.Lifted : Light.Lifted;
        public static Color Surface => IsDark ? Dark.Surface : Light.Surface;
        public static Color Primary => IsDark ? Dark.Primary : Light.Primary;
        public static Color OnPrimary => IsDark ? Dark.OnPrimary : Light.OnPrimary;
        public static Color Ink => IsDark ? Dark.Ink : Light.Ink;
        public static Color InkSoft => IsDark ? Dark.InkSoft : Light.InkSoft;
        public static Color Graphite => IsDark ? Dark.Graphite : Light.Graphite;
        public static Color Slate => IsDark ? Dark.Slate : Light.Slate;
        public static Color Stone => IsDark ? Dark.Stone : Light.Stone;
        public static Color Ash => IsDark ? Dark.Ash : Light.Ash;
        public static Color Hairline => IsDark ? Dark.Hairline : Light.Hairline;
        public static Color HairlineSoft => IsDark ? Dark.HairlineSoft : Light.HairlineSoft;
        public static Color Border => IsDark ? Dark.Border : Light.Border;
        public static Color Signal => IsDark ? Dark.Signal : Light.Signal;
        public static Color SignalLight => IsDark ? Dark.SignalLight : Light.SignalLight;
        public static Color Accent => IsDark ? Dark.Accent : Light.Accent;
        public static Color Scrim => IsDark ? Dark.Scrim : Light.Scrim;
        public static Color ScrimText => IsDark ? Dark.ScrimText : Light.ScrimText;
        public static Color Ghost => IsDark ? Dark.Ghost : Light.Ghost;
        // Legacy aliases kept so existing call sites compile unchanged
        public static Color CanvasWarm => Lifted;
        public static Color SlateSoft => Slate;
        public static Color Mute => Slate;
        // Typography — prefer modern Windows faces, fall back cleanly.
        // Display / UI: Segoe UI Variable → Bahnschrift → Segoe UI
        // Mono / log:   Cascadia Mono → Cascadia Code → Consolas
        public static readonly Font Display = PickFont(
            new[] { "Segoe UI Variable Display", "Bahnschrift SemiBold", "Segoe UI Semibold", "Segoe UI" },
            22f, FontStyle.Bold);
        public static readonly Font Body = PickFont(
            new[] { "Segoe UI Variable Text", "Bahnschrift", "Segoe UI" },
            10.25f, FontStyle.Regular);
        public static readonly Font ButtonFont = PickFont(
            new[] { "Segoe UI Variable Text", "Bahnschrift SemiBold", "Segoe UI Semibold", "Segoe UI" },
            9.75f, FontStyle.Bold);
        public static readonly Font Eyebrow = PickFont(
            new[] { "Segoe UI Variable Small", "Bahnschrift SemiBold", "Segoe UI Semibold", "Segoe UI" },
            8.25f, FontStyle.Bold);
        public static readonly Font Meta = PickFont(
            new[] { "Segoe UI Variable Text", "Bahnschrift", "Segoe UI" },
            9f, FontStyle.Regular);
        public static readonly Font Mono = PickFont(
            new[] { "Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Consolas", "Courier New" },
            9.5f, FontStyle.Regular);
        public static readonly Font MonoSmall = PickFont(
            new[] { "Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Consolas", "Courier New" },
            9f, FontStyle.Regular);
        /// <summary>First installed family wins. Uses InstalledFontCollection so GDI+ substitution can't fool us.</summary>
        private static Font PickFont(string[] families, float emSize, FontStyle style)
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var col = new System.Drawing.Text.InstalledFontCollection();
                foreach (var ff in col.Families)
                    installed.Add(ff.Name);
            }
            catch { }
            foreach (var family in families)
            {
                if (installed.Count > 0 && !installed.Contains(family))
                    continue;
                try
                {
                    if (style != FontStyle.Regular)
                    {
                        try { return new Font(family, emSize, style, GraphicsUnit.Point); }
                        catch { /* face may not expose this style */ }
                    }
                    var f = new Font(family, emSize, FontStyle.Regular, GraphicsUnit.Point);
                    // If collection was empty, still reject obvious substitutions
                    if (installed.Count == 0 &&
                        !string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                    {
                        f.Dispose();
                        continue;
                    }
                    return f;
                }
                catch { }
            }
            return new Font(SystemFonts.MessageBoxFont!.FontFamily, emSize,
                style == FontStyle.Regular ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Point);
        }
        // Radius scale: 20 (CTAs) / 40 (stadium) / 999 (pill) — nothing in between.
        public const int RButton = 20;
        public const int RStadium = 40;
        public const int RPill = 999;
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius >= 400 ? Math.Min(r.Width, r.Height) : Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        public static GraphicsPath Pill(Rectangle r) => RoundRect(r, RPill);
    }
    // Rounded lifted-cream card with the soft atmospheric halo (level-1/2 elevation).
    class CardPanel : Panel
    {
        public int Radius = Theme.RButton;
        public bool UseSurface;   // white pill (nav) instead of lifted cream
        public bool UseScrim;     // ink stadium (log console)
        public bool Shadow;       // floating-nav halo
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Color fill = UseScrim ? Theme.Scrim : UseSurface ? Theme.Surface : Theme.Lifted;
            if (Shadow)
            {
                // Atmospheric cushioning: stacked translucent rounded rects, no hard edge
                for (int i = 3; i >= 1; i--)
                {
                    var halo = new Rectangle(-i * 2 + 2, -i + 2, Width + i * 4 - 4, Height + i * 2 - 2);
                    using var hp = Theme.RoundRect(halo, Radius + i * 2);
                    using var hb = new SolidBrush(Color.FromArgb(7, Theme.Ink));
                    g.FillPath(hb, hp);
                }
            }
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundRect(rect, Radius))
            {
                using var b = new SolidBrush(fill);
                g.FillPath(b, path);
                if (!UseScrim)
                {
                    using var pen = new Pen(Theme.Hairline, 1f);
                    g.DrawPath(pen, path);
                }
            }
        }
    }
    // Primary — ink pill (20px radius, cream text). Secondary — surface pill with 1.5px ink border.
    class PillButton : Button
    {
        public bool Ghost { get; set; }
        private bool _hover;
        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.ButtonFont;
            Cursor = Cursors.Hand;
            Height = 34;
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null && Parent.BackColor.A > 0 ? Parent.BackColor : Theme.Canvas);
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = Theme.RoundRect(rect, Theme.RButton);
            bool disabled = !Enabled;
            Color fill, text;
            if (Ghost)
            {
                fill = disabled ? Theme.Surface : Theme.Surface;
                text = disabled ? Theme.Stone : Theme.Ink;
            }
            else
            {
                fill = disabled ? Theme.HairlineSoft : (_hover ? Theme.InkSoft : Theme.Primary);
                text = disabled ? Theme.Stone : Theme.OnPrimary;
            }
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (Ghost)
            {
                using var pen = new Pen(disabled ? Theme.HairlineSoft : Theme.Ink, 1.5f);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
    // Full-pill input: white field, 1px ink@50% border, deepens to 1.5px ink on focus.
    class FlatTextBox : Panel
    {
        public readonly TextBox Inner = new();
        private bool _focused;
        public FlatTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 34;
            Padding = new Padding(14, 0, 14, 0);
            Inner.BorderStyle = BorderStyle.None;
            Inner.Font = Theme.Body;
            Inner.ForeColor = Theme.Ink;
            Inner.BackColor = Theme.Surface;
            Inner.Multiline = false;
            Inner.Dock = DockStyle.Fill;
            Inner.Enter += (s, e) => { _focused = true; Invalidate(); };
            Inner.Leave += (s, e) => { _focused = false; Invalidate(); };
            Controls.Add(Inner);
            Resize += (s, e) => CenterInner();
            HandleCreated += (s, e) => CenterInner();
        }
        private void CenterInner()
        {
            int h = Math.Max(16, Inner.PreferredSize.Height);
            int top = Math.Max(0, (ClientSize.Height - h) / 2);
            Inner.Dock = DockStyle.None;
            Inner.Location = new Point(Padding.Left, top);
            Inner.Size = new Size(Math.Max(10, ClientSize.Width - Padding.Horizontal), h);
        }
        public new string Text { get => Inner.Text; set => Inner.Text = value; }
        public string PlaceholderText { get => Inner.PlaceholderText; set => Inner.PlaceholderText = value; }
        public new bool Enabled { get => Inner.Enabled; set { base.Enabled = value; Inner.Enabled = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.Pill(rect);
            using (var b = new SolidBrush(Inner.Enabled ? Theme.Surface : Theme.Canvas)) g.FillPath(b, path);
            using var pen = new Pen(_focused ? Theme.Ink : Theme.Border, _focused ? 1.5f : 1f);
            g.DrawPath(pen, path);
        }
    }
    // Ink ring radio — checked = solid ink circle with a cream core dot.
    class FlatRadio : RadioButton
    {
        public FlatRadio()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            AutoSize = false;
            Height = 22;
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null && Parent.BackColor.A > 0 ? Parent.BackColor : Theme.Canvas);
            const int d = 18;
            var circle = new Rectangle(1, (Height - d) / 2, d, d);
            if (Checked)
            {
                using var b = new SolidBrush(Enabled ? Theme.Ink : Theme.Ash);
                g.FillEllipse(b, circle);
                var core = new Rectangle(circle.X + 6, circle.Y + 6, d - 12, d - 12);
                using var cb = new SolidBrush(Theme.Canvas);
                g.FillEllipse(cb, core);
            }
            else
            {
                using var p = new Pen(Enabled ? Theme.Ink : Theme.Ash, 1.5f);
                g.DrawEllipse(p, circle);
            }
            var textRect = new Rectangle(d + 10, 0, Width - d - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Theme.Ink : Theme.Ash,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
    // 4px-radius ink checkbox with cream check.
    class FlatCheck : CheckBox
    {
        public FlatCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            AutoSize = false;
            Height = 22;
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            int d = 18;
            var box = new Rectangle(0, (Height - d) / 2, d, d);
            using (var path = Theme.RoundRect(box, 4))
            {
                if (Checked)
                {
                    using var b = new SolidBrush(Enabled ? Theme.Ink : Theme.Ash);
                    g.FillPath(b, path);
                    using var p = new Pen(Theme.Canvas, 2f);
                    g.DrawLines(p, new[]
                    {
                        new Point(box.X + 4, box.Y + 9),
                        new Point(box.X + 8, box.Y + 13),
                        new Point(box.X + 14, box.Y + 5)
                    });
                }
                else
                {
                    using var p = new Pen(Enabled ? Theme.Ink : Theme.Ash, 1.5f);
                    g.DrawPath(p, path);
                }
            }
            var textRect = new Rectangle(d + 10, 0, Width - d - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Theme.Ink : Theme.Ash,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
    enum NavIconKind
    {
        Document,
        Layers,
        Trophy
    }
    enum NavSegmentPosition
    {
        Start,
        Middle,
        End
    }
    // Single segment inside the stadium nav pill.
    // Active = blue icon/text + rounded top accent bar (modern segmented control).
    class NavSegment : Control
    {
        private bool _hover;
        private bool _active;
        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }
        public NavIconKind IconKind { get; set; } = NavIconKind.Document;
        public NavSegmentPosition SegmentPosition { get; set; } = NavSegmentPosition.Middle;
        public NavSegment()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.ButtonFont;
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Theme.Surface);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int w = Width;
            int h = Height;
            if (w < 4 || h < 4) return;
            // Exactly one indicator: a single centered, inset accent pill behind the
            // active segment (a subtler one on hover). No extra bars or lines.
            if (_active || _hover)
            {
                int alpha = _active ? (Theme.IsDark ? 40 : 24) : (Theme.IsDark ? 28 : 18);
                Color tint = _active ? Theme.Accent : Theme.Ink;
                var pill = new Rectangle(3, 2, w - 7, h - 5);
                using var path = Theme.Pill(pill);
                using var b = new SolidBrush(Color.FromArgb(alpha, tint));
                g.FillPath(b, path);
            }
            if (SegmentPosition != NavSegmentPosition.End)
            {
                int x = w - 1;
                int y1 = h / 4;
                int y2 = h - h / 4;
                using var pen = new Pen(Theme.HairlineSoft, 1f);
                g.DrawLine(pen, x, y1, x, y2);
            }
            Color fg = _active ? Theme.Accent : (_hover ? Theme.Ink : Theme.Graphite);
            const int iconSize = 16;
            const int gap = 8;
            var font = Font ?? Theme.ButtonFont;
            var textSize = TextRenderer.MeasureText(g, Text, font, new Size(int.MaxValue, h),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int total = iconSize + gap + textSize.Width;
            int startX = Math.Max(10, (w - total) / 2);
            int iconY = (h - iconSize) / 2;
            int textX = startX + iconSize + gap;
            DrawIcon(g, IconKind, new Rectangle(startX, iconY, iconSize, iconSize), fg);
            var textRect = new Rectangle(textX, 0, Math.Max(0, w - textX - 8), h);
            TextRenderer.DrawText(g, Text, font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        private static void DrawIcon(Graphics g, NavIconKind kind, Rectangle r, Color color)
        {
            using var pen = new Pen(color, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            float x = r.X;
            float y = r.Y;
            float s = r.Width;
            switch (kind)
            {
                case NavIconKind.Document:
                {
                    float m = s * 0.12f;
                    float fold = s * 0.32f;
                    using var path = new GraphicsPath();
                    path.StartFigure();
                    path.AddLine(x + m, y + m, x + s - m - fold, y + m);
                    path.AddLine(x + s - m - fold, y + m, x + s - m, y + m + fold);
                    path.AddLine(x + s - m, y + m + fold, x + s - m, y + s - m);
                    path.AddLine(x + s - m, y + s - m, x + m, y + s - m);
                    path.AddLine(x + m, y + s - m, x + m, y + m);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                    g.DrawLine(pen, x + s - m - fold, y + m, x + s - m - fold, y + m + fold);
                    g.DrawLine(pen, x + s - m - fold, y + m + fold, x + s - m, y + m + fold);
                    float lx1 = x + m + s * 0.18f;
                    float lx2 = x + s - m - s * 0.18f;
                    for (int i = 0; i < 3; i++)
                    {
                        float ly = y + s * 0.48f + i * s * 0.14f;
                        g.DrawLine(pen, lx1, ly, lx2, ly);
                    }
                    break;
                }
                case NavIconKind.Layers:
                {
                    void Diamond(float cy, float scale)
                    {
                        float cx = x + s * 0.5f;
                        float hw = s * 0.34f * scale;
                        float hh = s * 0.20f * scale;
                        var pts = new[]
                        {
                            new PointF(cx, cy - hh),
                            new PointF(cx + hw, cy),
                            new PointF(cx, cy + hh),
                            new PointF(cx - hw, cy)
                        };
                        g.DrawPolygon(pen, pts);
                    }
                    Diamond(y + s * 0.32f, 0.92f);
                    Diamond(y + s * 0.50f, 1.00f);
                    Diamond(y + s * 0.68f, 0.92f);
                    break;
                }
                case NavIconKind.Trophy:
                {
                    float m = s * 0.10f;
                    var bowl = new RectangleF(x + s * 0.22f, y + m, s * 0.56f, s * 0.42f);
                    g.DrawArc(pen, bowl, 200, 140);
                    g.DrawLine(pen, bowl.Left, y + m + s * 0.12f, bowl.Left, bowl.Top + bowl.Height * 0.55f);
                    g.DrawLine(pen, bowl.Right, y + m + s * 0.12f, bowl.Right, bowl.Top + bowl.Height * 0.55f);
                    g.DrawArc(pen, x + m * 0.4f, y + s * 0.16f, s * 0.28f, s * 0.32f, 90, 160);
                    g.DrawArc(pen, x + s - m * 0.4f - s * 0.28f, y + s * 0.16f, s * 0.28f, s * 0.32f, 330, 160);
                    float mid = x + s * 0.5f;
                    g.DrawLine(pen, mid, y + s * 0.52f, mid, y + s * 0.72f);
                    g.DrawLine(pen, mid - s * 0.12f, y + s * 0.72f, mid + s * 0.12f, y + s * 0.72f);
                    g.DrawLine(pen, mid - s * 0.22f, y + s - m, mid + s * 0.22f, y + s - m);
                    g.DrawLine(pen, mid - s * 0.16f, y + s * 0.78f, mid + s * 0.16f, y + s * 0.78f);
                    g.DrawLine(pen, mid - s * 0.16f, y + s * 0.78f, mid - s * 0.22f, y + s - m);
                    g.DrawLine(pen, mid + s * 0.16f, y + s * 0.78f, mid + s * 0.22f, y + s - m);
                    break;
                }
            }
        }
    }
// Signal-orange pill progress on a taupe track.
    class FlatProgressBar : Control
    {
        private int _value;
        public int Minimum = 0;
        public int Maximum = 100;
        public int Value { get => _value; set { _value = value; Invalidate(); } }
        public FlatProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 6;
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var tp = Theme.Pill(track))
            using (var tb = new SolidBrush(Theme.HairlineSoft))
                g.FillPath(tb, tp);
            double range = Math.Max(1, Maximum - Minimum);
            double frac = Math.Max(0, Math.Min(1.0, (Value - Minimum) / range));
            int w = (int)(Width * frac);
            if (w > Height)
            {
                var fillRect = new Rectangle(0, 0, w, Height - 1);
                using var fp = Theme.Pill(fillRect);
                using var fb = new SolidBrush(Theme.SignalLight);
                g.FillPath(fb, fp);
            }
        }
    }
}
