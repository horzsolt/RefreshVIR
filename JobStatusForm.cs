using System.Data;

namespace RefreshVIR
{
    public partial class JobStatusForm : Form
    {

        private DataGridView grid;
        private string connectionString;
        private Dictionary<string, string> jobs;
        private PictureBox? timelinePictureBox;
        private JobTimelineGrid? timelineGrid;
        private Panel timelinePanel;
        private SplitContainer splitContainer;
        private Panel loadingOverlay = null!;
        private Label loadingLabel = null!;
        private ProgressBar loadingProgressBar = null!;
        private Button refreshButton = null!;
        private List<JobExecution>? cachedTimelineHistory;
        private RadioButton oneDayRadio;
        private RadioButton oneWeekRadio;
        private int historyDays = 1;
        private bool suppressHistoryRangeEvents;
        private bool isLoading;

        /// <summary>
        /// Set to false to revert to the generated bitmap timeline chart.
        /// </summary>
        private const bool UseTimelineGrid = true;

        public JobStatusForm(string connectionString, Dictionary<string, string> jobNames)
        {
            InitializeComponent();
            ApplicationBrand.Apply(this);

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

            refreshButton = new Button
            {
                Text = "Frissítés",
                Dock = DockStyle.Top,
                Height = 40
            };
            refreshButton.Click += async (s, e) => await RefreshGridAsync();

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

            DataGridViewErrorHandler.Attach(grid, this, "Job státusz");

            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.Dock = DockStyle.Top;
            grid.Height = 420;

            timelinePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.DarkGray
            };

            if (UseTimelineGrid)
            {
                timelineGrid = new JobTimelineGrid
                {
                    Dock = DockStyle.Fill
                };
                timelinePanel.Controls.Add(timelineGrid);
            }
            else
            {
                timelinePictureBox = new PictureBox
                {
                    BackColor = Color.White,
                    SizeMode = PictureBoxSizeMode.Normal,
                    Dock = DockStyle.Fill
                };

                timelinePanel.Controls.Add(timelinePictureBox);
                timelinePanel.Resize += (s, e) => RenderTimelineImage();
            }

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

            CreateLoadingOverlay();

            Controls.Add(loadingOverlay);
            Controls.Add(splitContainer);
            Controls.Add(closeButton);
            Controls.Add(refreshButton);
            Controls.Add(historyRangePanel);

            Load += JobStatusForm_Load;
        }

        private void CreateLoadingOverlay()
        {
            loadingOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245),
                Visible = false
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Anchor = AnchorStyles.None
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            loadingLabel = new Label
            {
                Text = "Adatok betöltése...",
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(50, 50, 50),
                Margin = new Padding(0, 0, 0, 12),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            loadingProgressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 22,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            layout.Controls.Add(loadingLabel, 0, 0);
            layout.Controls.Add(loadingProgressBar, 0, 1);

            loadingOverlay.Controls.Add(layout);
            loadingOverlay.Resize += (_, _) => CenterLoadingContent(layout);
            CenterLoadingContent(layout);
        }

        private void CenterLoadingContent(Control content)
        {
            content.Location = new Point(
                Math.Max(0, (loadingOverlay.ClientSize.Width - content.Width) / 2),
                Math.Max(0, (loadingOverlay.ClientSize.Height - content.Height) / 2));
        }

        private void ShowLoading(string message, int percent)
        {
            loadingLabel.Text = message;
            loadingProgressBar.Value = Math.Clamp(percent, loadingProgressBar.Minimum, loadingProgressBar.Maximum);
            loadingOverlay.Visible = true;
            loadingOverlay.BringToFront();
            refreshButton.Enabled = false;
            oneDayRadio.Enabled = false;
            oneWeekRadio.Enabled = false;
            UseWaitCursor = true;
            loadingOverlay.Update();
        }

        private void HideLoading()
        {
            loadingOverlay.Visible = false;
            refreshButton.Enabled = true;
            oneDayRadio.Enabled = true;
            oneWeekRadio.Enabled = true;
            UseWaitCursor = false;
        }

        private async void HistoryRange_CheckedChanged(object? sender, EventArgs e)
        {
            if (suppressHistoryRangeEvents || isLoading)
                return;

            if (sender is not RadioButton radio || !radio.Checked)
                return;

            int selectedDays = oneWeekRadio.Checked ? 7 : 1;
            if (selectedDays == historyDays)
                return;

            historyDays = selectedDays;

            string rangeLabel = historyDays == 1 ? "1 napos adat" : "1 hetes adat";
            SQLUtils.LogAction($"Megjelenítési időszak módosítva: {rangeLabel}");

            await ReloadViewDataAsync();
        }

        private Task ReloadViewDataAsync() =>
            LoadViewDataAsync(configureSplitter: false);

        private void JobStatusForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SQLUtils.LogAction("Státusz ablak bezárva");
        }

        private async void JobStatusForm_Load(object sender, EventArgs e)
        {
            suppressHistoryRangeEvents = true;

            try
            {
                await LoadViewDataAsync(configureSplitter: true);
            }
            finally
            {
                suppressHistoryRangeEvents = false;
            }
        }

        private async Task LoadViewDataAsync(bool configureSplitter)
        {
            if (isLoading)
                return;

            isLoading = true;

            try
            {
                ShowLoading("Job státusz adatok betöltése...", 10);
                await Task.Yield();

                DataTable jobDetails = await Task.Run(() =>
                    SQLUtils.GetJobDetails(connectionString, jobs, historyDays));

                ShowLoading("Job státusz adatok megjelenítése...", 40);
                await Task.Yield();
                BindGridData(jobDetails);

                if (configureSplitter)
                    splitContainer.SplitterDistance = grid.Height;

                ShowLoading("Futástörténet betöltése...", 60);
                await Task.Yield();

                int days = historyDays;
                List<JobExecution> history = await Task.Run(() =>
                    SQLUtils.GetJobExecutionHistory(connectionString, jobs, days));

                ShowLoading("Idővonal megjelenítése...", 85);
                await Task.Yield();

                cachedTimelineHistory = history;
                RenderTimeline();

                ShowLoading("Kész", 100);
                await Task.Yield();
            }
            finally
            {
                HideLoading();
                isLoading = false;
            }
        }

        private void BindGridData(DataTable? jobDetails = null)
        {
            grid.DataSource = jobDetails
                ?? SQLUtils.GetJobDetails(connectionString, jobs, historyDays);

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

        private async void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (grid.Columns[e.ColumnIndex].Name == "Action")
            {
                string jobName = grid.Rows[e.RowIndex].Cells["Job neve"].Value.ToString();
                string status = grid.Rows[e.RowIndex].Cells["Jelenlegi státusz"].Value?.ToString();

                bool isRunning = status == "Running";

                if (!isRunning && !Authorization.ConfirmAllowedToStartJobs(this))
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

                await RefreshGridAsync();
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

        private async Task RefreshGridAsync()
        {
            SQLUtils.LogAction("Adatok frissítése (Státusz ablak)");
            await LoadViewDataAsync(configureSplitter: false);
        }

        private void RenderTimeline()
        {
            if (cachedTimelineHistory == null)
                return;

            if (UseTimelineGrid)
            {
                timelineGrid?.Bind(cachedTimelineHistory, historyDays);
                return;
            }

            RenderTimelineImage();
        }

        private void RenderTimelineImage()
        {
            if (cachedTimelineHistory == null || timelinePictureBox == null)
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
