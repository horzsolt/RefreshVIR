using System.Data;

namespace RefreshVIR
{
    public partial class JobStatusForm : Form
    {

        private DataGridView grid;
        private string connectionString;
        private Dictionary<string, string> jobs;
        private PictureBox timelinePictureBox;
        private SplitContainer splitContainer;

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

            Button refreshButton = new Button
            {
                Text = "Frissítés",
                Dock = DockStyle.Top,
                Height = 40
            };
            refreshButton.Click += (s, e) => this.RefreshGrid();

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
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            grid.ColumnHeadersDefaultCellStyle.Font =
                new Font(grid.Font, FontStyle.Bold);

            grid.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            grid.CellClick += Grid_CellClick;

            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.Dock = DockStyle.Top;
            grid.Height = 420;

            timelinePictureBox = new PictureBox
            {
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.AutoSize,
                Dock = DockStyle.Top
            };

            Panel timelinePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.DarkGray
            };

            timelinePanel.Controls.Add(timelinePictureBox);

            splitContainer =
                new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Horizontal,

                    // Top = grid
                    // Bottom = timeline
                    SplitterDistance = 0,

                    Panel1MinSize = 150,
                    Panel2MinSize = 150
                };

            splitContainer.Panel1.Controls.Add(grid);
            splitContainer.Panel2.Controls.Add(timelinePanel);

            Controls.Add(splitContainer);
            Controls.Add(closeButton);
            Controls.Add(refreshButton);

            Load += JobStatusForm_Load;
        }

        private void JobStatusForm_Load(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                DataTable dt = SQLUtils.GetJobDetails(connectionString, jobs, 14);
                grid.DataSource = dt;

                grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns[5].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns[8].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                grid.Columns[0].Width = 300;

                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Cells["Jelenlegi státusz"].Value?.ToString() == "Running")
                    {
                        row.DefaultCellStyle.BackColor = Color.Gold;
                    }
                }

                var btnCol = new DataGridViewButtonColumn
                {
                    Name = "Action",
                    HeaderText = "Művelet",
                    Text = "Stop",
                    UseColumnTextForButtonValue = false
                };

                grid.Columns.Add(btnCol);
                UpdateButtons();

                RefreshTimeline();
                splitContainer.SplitterDistance = grid.Height;
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
                UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                var status = row.Cells["Jelenlegi státusz"].Value?.ToString();

                row.Cells["Action"].Value =
                    status == "Running" ? "Stop" : "Start";

                if (row.Cells["Jelenlegi státusz"].Value?.ToString() == "Running")
                {
                    row.DefaultCellStyle.BackColor = Color.Gold;
                }
            }
        }

        private void RefreshGrid()
        {
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                grid.DataSource = SQLUtils.GetJobDetails(connectionString, jobs, 14);

                List<JobExecution> history =
                    SQLUtils.GetJobExecutionHistory(
                        connectionString,
                        jobs);
                Bitmap bmp =
                    JobTimelineRenderer.CreateJobTimelineChart(history,
                        timelinePictureBox.Width);

                timelinePictureBox.Image?.Dispose();
                timelinePictureBox.Image = bmp;

                UpdateButtons();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void RefreshTimeline()
        {
            List<JobExecution> history =
                SQLUtils.GetJobExecutionHistory(
                    connectionString,
                    jobs);
            Bitmap bmp =
                JobTimelineRenderer.CreateJobTimelineChart(history,
                    timelinePictureBox.Width);

            timelinePictureBox.Image?.Dispose();
            timelinePictureBox.Image = bmp;
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
