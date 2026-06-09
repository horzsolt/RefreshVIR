using System.Data;

namespace RefreshVIR
{
    public partial class JobStatusForm : Form
    {

        private DataGridView grid;
        private string connectionString;
        private Dictionary<string, string> jobs;
        private PictureBox timelinePictureBox;
        private Panel timelinePanel;
        private SplitContainer splitContainer;
        private List<JobExecution>? cachedTimelineHistory;
        private RadioButton oneDayRadio;
        private RadioButton oneWeekRadio;
        private int historyDays = 1;
        private bool suppressHistoryRangeEvents;

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

            Panel historyRangePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };

            oneDayRadio = new RadioButton
            {
                Text = "1 napos adat",
                Checked = true,
                Left = 10,
                Top = 8,
                AutoSize = true
            };
            oneDayRadio.CheckedChanged += HistoryRange_CheckedChanged;

            oneWeekRadio = new RadioButton
            {
                Text = "1 hetes adat",
                Left = 150,
                Top = 8,
                AutoSize = true
            };
            oneWeekRadio.CheckedChanged += HistoryRange_CheckedChanged;

            historyRangePanel.Controls.Add(oneDayRadio);
            historyRangePanel.Controls.Add(oneWeekRadio);

            this.FormClosed += JobStatusForm_FormClosed;

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
                SizeMode = PictureBoxSizeMode.Normal,
                Dock = DockStyle.Fill
            };

            timelinePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.DarkGray
            };

            timelinePanel.Controls.Add(timelinePictureBox);
            timelinePanel.Resize += (s, e) => RenderTimeline();

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
            Controls.Add(historyRangePanel);

            Load += JobStatusForm_Load;
        }

        private void HistoryRange_CheckedChanged(object? sender, EventArgs e)
        {
            if (suppressHistoryRangeEvents)
                return;

            if (sender is not RadioButton radio || !radio.Checked)
                return;

            int selectedDays = oneWeekRadio.Checked ? 7 : 1;
            if (selectedDays == historyDays)
                return;

            historyDays = selectedDays;

            string rangeLabel = historyDays == 1 ? "1 napos adat" : "1 hetes adat";
            SQLUtils.LogAction($"Megjelenítési időszak módosítva: {rangeLabel}");

            ReloadViewData();
        }

        private void ReloadViewData()
        {
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                BindGridData();
                RefreshTimeline();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void JobStatusForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SQLUtils.LogAction("Státusz ablak bezárva");
        }

        private void JobStatusForm_Load(object sender, EventArgs e)
        {
            try
            {
                suppressHistoryRangeEvents = true;
                Cursor.Current = Cursors.WaitCursor;

                BindGridData();
                splitContainer.SplitterDistance = grid.Height;
                RefreshTimeline();
            }
            finally
            {
                suppressHistoryRangeEvents = false;
                Cursor.Current = Cursors.Default;
            }
        }

        private void BindGridData()
        {
            grid.DataSource = SQLUtils.GetJobDetails(connectionString, jobs, historyDays);

            grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[5].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[8].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            grid.Columns[0].Width = 300;

            EnsureActionColumn();
            UpdateButtons();
        }

        private void EnsureActionColumn()
        {
            if (grid.Columns.Contains("Action"))
                return;

            grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "Művelet",
                Text = "Stop",
                UseColumnTextForButtonValue = false
            });
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (grid.Columns[e.ColumnIndex].Name == "Action")
            {
                string jobName = grid.Rows[e.RowIndex].Cells["Job neve"].Value.ToString();
                string status = grid.Rows[e.RowIndex].Cells["Jelenlegi státusz"].Value?.ToString();

                bool isRunning = status == "Running";

                if (!isRunning && !JobAuthorization.ConfirmAllowedToStartJobs(this))
                    return;

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
                bool isRunning = status == "Running";

                row.Cells["Action"].Value = isRunning ? "Stop" : "Start";
                row.DefaultCellStyle.BackColor = isRunning ? Color.Gold : Color.White;
            }
        }

        private void RefreshGrid()
        {
            SQLUtils.LogAction("Adatok frissítése (Státusz ablak)");

            Cursor.Current = Cursors.WaitCursor;

            try
            {
                BindGridData();

                cachedTimelineHistory =
                    SQLUtils.GetJobExecutionHistory(
                        connectionString,
                        jobs,
                        historyDays);
                RenderTimeline();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void RefreshTimeline()
        {
            cachedTimelineHistory =
                SQLUtils.GetJobExecutionHistory(
                    connectionString,
                    jobs,
                    historyDays);
            RenderTimeline();
        }

        private void RenderTimeline()
        {
            if (cachedTimelineHistory == null)
                return;

            int width = Math.Max(1, timelinePanel.ClientSize.Width);
            int height = Math.Max(1, timelinePanel.ClientSize.Height);

            Bitmap bmp =
                JobTimelineRenderer.CreateJobTimelineChart(
                    cachedTimelineHistory,
                    width,
                    height,
                    historyDays);

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
