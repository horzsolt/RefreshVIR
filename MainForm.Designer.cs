namespace RefreshVIR
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private TableLayoutPanel mainLayout;
        private Panel headerPanel;
        private PictureBox logoBox;
        private Label titleLabel;
        private Label sessionLabel;
        private Button btnExit;
        private TableLayoutPanel contentLayout;
        private TableLayoutPanel cardsLayout;
        private DashboardCard cardStatus;
        private DashboardCard cardRefreshGL;
        private DashboardCard cardPowerBI;
        private DashboardCard cardElabe;
        private ProgressBar progressBar1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            logoBox = new PictureBox();
            titleLabel = new Label();
            sessionLabel = new Label();
            btnExit = new Button();
            headerPanel = new Panel();
            cardStatus = new DashboardCard("Státusz", "Adatfrissítő jobok állapota, indítás és leállítás");
            cardRefreshGL = new DashboardCard("Főkönyv", "Főkönyvi adatok kézi frissítése");
            cardPowerBI = new DashboardCard("Power BI", "Riport publikálása a felhőbe");
            cardElabe = new DashboardCard("ELABE editor", "ELABE tábla szerkesztése");
            cardsLayout = new TableLayoutPanel();
            contentLayout = new TableLayoutPanel();
            progressBar1 = new ProgressBar();
            mainLayout = new TableLayoutPanel();

            headerPanel.SuspendLayout();
            cardsLayout.SuspendLayout();
            contentLayout.SuspendLayout();
            mainLayout.SuspendLayout();
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Font;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            BackColor = Color.FromArgb(245, 246, 248);
            DoubleBuffered = true;

            // header
            headerPanel.Dock = DockStyle.Fill;
            headerPanel.BackColor = Color.FromArgb(6, 110, 118);
            headerPanel.Padding = new Padding(28, 14, 20, 14);

            logoBox.Size = new Size(400, 68);
            logoBox.Location = new Point(24, 12);
            logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            logoBox.BackColor = Color.Transparent;
            logoBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            TryLoadLogo();

            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(440, 26);
            titleLabel.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.Text = "Controlling Vezérlőpult";
            titleLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            sessionLabel.AutoSize = false;
            sessionLabel.Size = new Size(360, 28);
            sessionLabel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            sessionLabel.TextAlign = ContentAlignment.MiddleRight;
            sessionLabel.Font = new Font("Segoe UI", 9.5f);
            sessionLabel.ForeColor = Color.FromArgb(220, 222, 226);
            sessionLabel.BackColor = Color.Transparent;

            btnExit.Text = "Kilépés";
            btnExit.Size = new Size(100, 34);
            btnExit.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderColor = Color.FromArgb(40, 140, 148);
            btnExit.Font = new Font("Segoe UI", 9.5f);
            btnExit.ForeColor = Color.White;
            btnExit.BackColor = Color.FromArgb(6, 110, 118);
            btnExit.Cursor = Cursors.Hand;
            btnExit.Click += btnExit_Click;

            headerPanel.Controls.Add(logoBox);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(sessionLabel);
            headerPanel.Controls.Add(btnExit);
            headerPanel.Resize += (_, _) => LayoutHeader();

            // cards
            cardsLayout.ColumnCount = 2;
            cardsLayout.RowCount = 2;
            cardsLayout.Dock = DockStyle.Fill;
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            cardStatus.Dock = DockStyle.Fill;
            cardRefreshGL.Dock = DockStyle.Fill;
            cardPowerBI.Dock = DockStyle.Fill;
            cardElabe.Dock = DockStyle.Fill;
            cardStatus.Click += button1_Click;
            cardRefreshGL.Click += btnRefreshGL_Click;
            cardPowerBI.Click += btnPowerBI_Click;
            cardElabe.Click += btnElabe_Click;
            cardsLayout.Controls.Add(cardStatus, 0, 0);
            cardsLayout.Controls.Add(cardRefreshGL, 1, 0);
            cardsLayout.Controls.Add(cardPowerBI, 0, 1);
            cardsLayout.Controls.Add(cardElabe, 1, 1);

            contentLayout.Dock = DockStyle.Fill;
            contentLayout.BackColor = Color.FromArgb(245, 246, 248);
            contentLayout.ColumnCount = 3;
            contentLayout.RowCount = 3;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1280F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 640F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            contentLayout.Controls.Add(cardsLayout, 1, 1);

            progressBar1.Dock = DockStyle.Fill;
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.Visible = false;
            progressBar1.MarqueeAnimationSpeed = 30;

            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 1;
            mainLayout.RowCount = 3;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.Controls.Add(contentLayout, 0, 1);
            mainLayout.Controls.Add(progressBar1, 0, 2);
            mainLayout.Paint += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(4, 88, 94));
                e.Graphics.DrawLine(pen, 0, 91, mainLayout.Width, 91);
            };

            Controls.Add(mainLayout);

            headerPanel.ResumeLayout(false);
            cardsLayout.ResumeLayout(false);
            contentLayout.ResumeLayout(false);
            mainLayout.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private void LayoutHeader()
        {
            btnExit.Location = new Point(headerPanel.ClientSize.Width - btnExit.Width - 20, 23);
            sessionLabel.Location = new Point(btnExit.Left - sessionLabel.Width - 16, 26);
        }

        private void TryLoadLogo()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "gw.png");
            if (!File.Exists(path))
            {
                logoBox.Visible = false;
                titleLabel.Location = new Point(28, 22);
                return;
            }

            logoBox.Image = Image.FromFile(path);
        }
    }
}
