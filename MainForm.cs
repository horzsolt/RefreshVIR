using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace RefreshVIR
{
    public partial class MainForm : Form
    {
        string connectionString = $"Server={Environment.GetEnvironmentVariable("VIR_SQL_SERVER_NAME")};" +
                          //$"Database={Environment.GetEnvironmentVariable("VIR_SQL_DATABASE")};" +
                          $"Database=qad;" +
                          $"User Id={Environment.GetEnvironmentVariable("VIR_SQL_USER")};" +
                          $"Password={Environment.GetEnvironmentVariable("VIR_SQL_PASSWORD")};" +
                          "Connection Timeout=500;Trust Server Certificate=true";

        public MainForm()
        {
            InitializeComponent();
        }

        private async void btnRefreshGL_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show(
                "Biztosan elindítod a fõkönyv frissítését?",
                "Frissítés megerõsítése",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            DisableButtons();
            this.Cursor = Cursors.WaitCursor;
            Application.DoEvents();

            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.Visible = true;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                await Task.Run(() => ExecuteStoredProcedure("sp_Refresh_General_Ledger"));

                stopwatch.Stop();
                MessageBox.Show($"A kiválasztott frissítés sikeresen lefutott {stopwatch.Elapsed.TotalSeconds:F2} mp alatt.",
                                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MessageBox.Show($"Hiba történt:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                progressBar1.Visible = false;
                EnableButtons();
            }
        }

        private void EnableButtons()
        {
            btnRefreshGL.Enabled = true;
            btnExit.Enabled = true;
        }

        private void DisableButtons()
        {
            btnRefreshGL.Enabled = false;
            btnExit.Enabled = false;
        }

        private void ExecuteStoredProcedure(string procedureName)
        {
            using SqlConnection conn = new SqlConnection(connectionString);
            using SqlCommand cmd = new SqlCommand(procedureName, conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 0
            };

            conn.Open();
            cmd.ExecuteNonQuery();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
