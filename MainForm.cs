using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace RefreshVIR
{
    public partial class MainForm : Form
    {
        string connectionString = $"Server={Environment.GetEnvironmentVariable("VIR_SQL_SERVER_NAME")};" +
                          $"Database=qad;" +
                          $"User Id={Environment.GetEnvironmentVariable("VIR_SQL_USER")};" +
                          $"Password={Environment.GetEnvironmentVariable("VIR_SQL_PASSWORD")};" +
                          "Connection Timeout=500;Trust Server Certificate=true";

        Dictionary<string, string> jobs = new Dictionary<string, string>
            {
                { "QAD_GL_frissites", "Fõkönyv teljes frissítés" },
                { "QAD_GL_INC_frissites", "Fõkönyv növekményes frissítés" },
                { "QAD_VIR_2025_frissites", "QAD frissítés 1." },
                { "QAD_VIR_2025_frissites_2", "QAD frissítés 2." },
                { "QAD_VIR_2025_frissites_3", "QAD frissítés 3." },
                { "Refresh_Scriptor_1", "Scriptor frissítés 1." },
                { "Refresh_Scriptor_2", "Scriptor frissítés 2." },
                { "Refresh_Scriptor_3", "Scriptor frissítés 3." },
                { "Refresh_Scriptor_4", "Scriptor frissítés 4." }
            };

        public MainForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Controlling vezérlõpult";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
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
            GLRefreshForm glForm = new GLRefreshForm(connectionString);
            glForm.Show();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            JobStatusForm jobForm = new JobStatusForm(connectionString, jobs);
            jobForm.Show();
        }
    }
}
