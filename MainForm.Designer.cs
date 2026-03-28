namespace RefreshVIR
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnRefreshGL;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.TableLayoutPanel mainLayout;
        private System.Windows.Forms.Label titleLabel;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnRefreshGL = new Button();
            this.btnExit = new Button();
            this.button1 = new Button();
            this.progressBar1 = new ProgressBar();
            this.mainLayout = new TableLayoutPanel();
            this.titleLabel = new Label();

            this.mainLayout.SuspendLayout();
            this.SuspendLayout();

            // 
            // MainForm
            // 
            this.AutoScaleMode = AutoScaleMode.Font;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Controlling vezérlőpult";
            this.KeyPreview = true;

            // 
            // titleLabel
            // 
            this.titleLabel = new Label();
            this.titleLabel.Text = "Controlling Vezérlőpult";
            this.titleLabel.Dock = DockStyle.Fill;
            this.titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.titleLabel.Font = new Font("Segoe UI", 26, FontStyle.Bold);
            this.titleLabel.Height = 100;

            // 
            // btnRefreshGL
            // 
            this.btnRefreshGL.Text = "Főkönyv";
            this.btnRefreshGL.Dock = DockStyle.Fill;
            this.btnRefreshGL.Margin = new Padding(30);
            this.btnRefreshGL.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            this.btnRefreshGL.BackColor = Color.SteelBlue;
            this.btnRefreshGL.ForeColor = Color.White;
            this.btnRefreshGL.FlatStyle = FlatStyle.Flat;
            this.btnRefreshGL.Click += btnRefreshGL_Click;

            // 
            // button1 (Status)
            // 
            this.button1.Text = "Státusz";
            this.button1.Dock = DockStyle.Fill;
            this.button1.Margin = new Padding(30);
            this.button1.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            this.button1.BackColor = Color.DarkSlateGray;
            this.button1.ForeColor = Color.White;
            this.button1.FlatStyle = FlatStyle.Flat;
            this.button1.Click += button1_Click;

            // 
            // btnExit
            // 
            this.btnExit.Text = "Exit";
            this.btnExit.Dock = DockStyle.Fill;
            this.btnExit.Margin = new Padding(30);
            this.btnExit.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            this.btnExit.BackColor = Color.Firebrick;
            this.btnExit.ForeColor = Color.White;
            this.btnExit.FlatStyle = FlatStyle.Flat;
            this.btnExit.Click += btnExit_Click;

            // 
            // progressBar1
            // 
            this.progressBar1.Dock = DockStyle.Bottom;
            this.progressBar1.Height = 25;
            this.progressBar1.Style = ProgressBarStyle.Marquee;
            this.progressBar1.Visible = false;
            this.progressBar1.MarqueeAnimationSpeed = 30;

            // 
            // mainLayout
            // 
            this.mainLayout.ColumnCount = 3;
            this.mainLayout.RowCount = 3;
            this.mainLayout.Dock = DockStyle.Fill;
            this.mainLayout.BackColor = Color.White;
            this.mainLayout.Padding = new Padding(80);

            this.mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            this.mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            this.mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            this.mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));   // title
            this.mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // buttons
            this.mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));   // progress

            // 
            // Layout placement
            // 
            this.mainLayout.Controls.Add(this.titleLabel, 0, 0);
            this.mainLayout.SetColumnSpan(this.titleLabel, 3);

            this.mainLayout.Controls.Add(this.button1, 0, 1);
            this.mainLayout.Controls.Add(this.btnRefreshGL, 1, 1);
            this.mainLayout.Controls.Add(this.btnExit, 2, 1);

            this.mainLayout.Controls.Add(this.progressBar1, 0, 2);
            this.mainLayout.SetColumnSpan(this.progressBar1, 3);

            // 
            // MainForm controls
            // 
            this.Controls.Add(this.mainLayout);

            this.mainLayout.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private Button button1;
    }
}
