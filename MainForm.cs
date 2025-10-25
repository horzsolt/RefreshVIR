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
                { "QAD_GL_frissites", "F�k�nyv teljes friss�t�s" },
                { "QAD_GL_INC_frissites", "F�k�nyv n�vekm�nyes friss�t�s" },
                { "QAD_VIR_2025_frissites", "QAD friss�t�s 1." },
                { "QAD_VIR_2025_frissites_2", "QAD friss�t�s 2." },
                { "QAD_VIR_2025_frissites_3", "QAD friss�t�s 3." },
                { "Refresh_Scriptor_1", "Scriptor friss�t�s 1." },
                { "Refresh_Scriptor_2", "Scriptor friss�t�s 2." },
                { "Refresh_Scriptor_3", "Scriptor friss�t�s 3." },
                { "Refresh_Scriptor_4", "Scriptor friss�t�s 4." }
            };

        public MainForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Controlling vez�rl�pult";
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
