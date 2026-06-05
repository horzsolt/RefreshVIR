namespace RefreshVIR
{
    public partial class MainForm : Form
    {

        public MainForm()
        {
            InitializeComponent();
            ApplyCaption();
            this.KeyDown += MainForm_KeyDown;
        }

        private void ApplyCaption()
        {
            string server =
                Environment.GetEnvironmentVariable("VIR_SQL_SERVER_NAME") ?? "n/a";

            string caption =
                $"{Environment.UserDomainName}-{Environment.UserName} [{server}]";

            this.Text = caption;
            this.titleLabel.Text =
                $"Controlling Vezérlőpult{Environment.NewLine}{caption}";
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private async void btnRefreshGL_Click(object sender, EventArgs e)
        {
            GLRefreshForm glForm = new GLRefreshForm(Configuration.connectionString);
            glForm.Show();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            JobStatusForm jobForm = new JobStatusForm(Configuration.connectionString, Configuration.jobs);
            jobForm.Show();
        }
    }
}
