namespace RefreshVIR
{
    internal sealed class DashboardCard : Panel
    {
        private static readonly Color BorderColor = Color.FromArgb(220, 222, 226);
        private static readonly Color HoverFill = Color.FromArgb(255, 246, 246);
        private static readonly Color Accent = Color.FromArgb(217, 33, 40);

        private readonly Label _title;
        private readonly Label _description;
        private bool _hot;

        public DashboardCard(string title, string description)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            Margin = new Padding(16);
            Padding = new Padding(36, 36, 32, 32);

            _title = new Label
            {
                Text = title,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 52,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 45, 48),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _description = new Label
            {
                Text = description,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12.5f),
                ForeColor = Color.FromArgb(88, 89, 91),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            Controls.Add(_description);
            Controls.Add(_title);

            MouseEnter += (_, _) => SetHot(true);
            MouseLeave += (_, _) => SetHot(false);
            _title.MouseEnter += (_, _) => SetHot(true);
            _description.MouseEnter += (_, _) => SetHot(true);
            _title.MouseLeave += (_, _) =>
            {
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    SetHot(false);
            };
            _description.MouseLeave += (_, _) =>
            {
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    SetHot(false);
            };

            _title.Click += (_, e) => OnClick(e);
            _description.Click += (_, e) => OnClick(e);
        }

        private void SetHot(bool hot)
        {
            if (_hot == hot)
                return;
            _hot = hot;
            BackColor = hot ? HoverFill : Color.White;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var border = new Pen(BorderColor);
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            if (!_hot)
                return;

            using var accent = new SolidBrush(Accent);
            e.Graphics.FillRectangle(accent, 0, 0, 5, Height);
        }
    }
}
