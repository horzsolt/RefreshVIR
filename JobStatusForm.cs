using System.Data;

namespace RefreshVIR
{
    public partial class JobStatusForm : Form
    {

        private DataGridView grid;
        private string connectionString;
        private Dictionary<string, string> jobs;

        public JobStatusForm(string connectionString, Dictionary<string, string> jobNames)
        {
            InitializeComponent();

            this.connectionString = connectionString;
            this.jobs = jobNames;

            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Frissítő jobok státusza";

            this.KeyPreview = true;
            this.KeyDown += JobStatusForm_KeyDown;

            Button closeButton = new Button
            {
                Text = "<< Vissza",
                Dock = DockStyle.Bottom,   // makes it full width at the bottom
                Height = 40                // fixed height, you can adjust
            };
            closeButton.Click += (s, e) => this.Close();

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            grid.AllowUserToAddRows = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            grid.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font(grid.Font, System.Drawing.FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.CellClick += Grid_CellClick;

            Controls.Add(grid);
            Controls.Add(closeButton);

            Load += JobStatusForm_Load;
        }

        private void JobStatusForm_Load(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                DataTable dt = SQLUtils.GetJobDetails(connectionString, jobs, 14);
                grid.DataSource = dt;
                grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                var btnCol = new DataGridViewButtonColumn
                {
                    Name = "Action",
                    HeaderText = "Művelet",
                    Text = "Stopp",
                    UseColumnTextForButtonValue = false
                };

                grid.Columns.Add(btnCol);
                UpdateButtons();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (grid.Columns[e.ColumnIndex].Name == "Action")
            {
                string jobName = grid.Rows[e.RowIndex].Cells["Job neve"].Value.ToString();
                string status = grid.Rows[e.RowIndex].Cells["Jelenlegi státusz"].Value?.ToString();

                bool isRunning = status == "Running";

                string actionText = isRunning ? "leállítani" : "elindítani";
                string actionTitle = isRunning ? "Job leállítása" : "Job indítása";

                var result = MessageBox.Show(
                    $"Biztosan szeretnéd {actionText} ezt a jobot?\n\n{jobName}",
                    actionTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;

                if (isRunning)
                {
                    SQLUtils.StopJob(jobName, connectionString);
                }
                else
                {
                    SQLUtils.StartJob(jobName, connectionString);
                }

                RefreshGrid();
            }
        }

        private void UpdateButtons()
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                var status = row.Cells["Jelenlegi státusz"].Value?.ToString();

                row.Cells["Action"].Value =
                    status == "Running" ? "Stop" : "Start";
            }
        }

        private void RefreshGrid()
        {
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                grid.DataSource = SQLUtils.GetJobDetails(connectionString, jobs, 14);
                UpdateButtons();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void JobStatusForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }
    }
}
