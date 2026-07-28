namespace RefreshVIR
{
    /// <summary>
    /// Interactive timeline grid: rows are jobs, columns are hourly time slots.
    /// </summary>
    public sealed class JobTimelineGrid : UserControl
    {
        private const int JobColumnWidth = 180;
        private const int MinTimeColumnWidth = 32;
        private const int HeaderHeight = 44;

        private static readonly Font HeaderHourFont = new("Segoe UI", 8, FontStyle.Regular);
        private static readonly Font HeaderDateFont = new("Segoe UI", 8, FontStyle.Bold);
        private static readonly Color HeaderHourBackColor = Color.FromArgb(235, 235, 235);
        private static readonly Color HeaderHourForeColor = Color.FromArgb(70, 70, 70);
        private static readonly Color HeaderDateBackColor = Color.FromArgb(198, 210, 232);
        private static readonly Color HeaderDateForeColor = Color.FromArgb(20, 50, 100);

        private readonly DataGridView _grid;
        private readonly ToolTip _toolTip;
        private int _historyDays = 1;

        public JobTimelineGrid()
        {
            _toolTip = new ToolTip
            {
                AutoPopDelay = 60000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true,
                IsBalloon = false,
                UseFading = true
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Vertical,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = HeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    SelectionBackColor = Color.FromArgb(200, 220, 255),
                    SelectionForeColor = Color.Black
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderHourBackColor,
                    ForeColor = HeaderHourForeColor,
                    Font = HeaderHourFont,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True
                }
            };

            _grid.Resize += (_, _) => ApplyHorizontalStretch(_historyDays);
            _grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
            _toolTip.SetToolTip(_grid, string.Empty);
            DataGridViewErrorHandler.Attach(_grid, FindForm(), "Idővonal");
            Controls.Add(_grid);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _toolTip.Dispose();

            base.Dispose(disposing);
        }

        private void Grid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                e.ToolTipText = string.Empty;
                return;
            }

            DataGridViewCell cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            e.ToolTipText = string.IsNullOrWhiteSpace(cell.ToolTipText)
                ? string.Empty
                : cell.ToolTipText;
        }

        public void Bind(IReadOnlyList<JobExecution> executions, int historyDays)
        {
            _historyDays = historyDays;

            _grid.SuspendLayout();
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            (DateTime timelineStart, DateTime timelineEnd) = GetTimelineRange(historyDays);
            List<DateTime> hourSlots = BuildHourSlots(timelineStart, timelineEnd);

            DataGridViewTextBoxColumn jobColumn = new DataGridViewTextBoxColumn
            {
                Name = "JobName",
                HeaderText = "Job",
                Frozen = true,
                Width = JobColumnWidth,
                MinimumWidth = JobColumnWidth,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    BackColor = Color.WhiteSmoke,
                    Font = new Font("Segoe UI", 8, FontStyle.Regular)
                }
            };
            _grid.Columns.Add(jobColumn);

            for (int i = 0; i < hourSlots.Count; i++)
            {
                DateTime slotStart = hourSlots[i];
                DateTime? previousSlot = i > 0 ? hourSlots[i - 1] : null;
                bool isDateHeader = IsDateHeaderColumn(slotStart, historyDays, previousSlot);

                TimelineHourColumn timeColumn = new TimelineHourColumn
                {
                    Name = $"T{i}",
                    HeaderText = FormatTimeColumnHeader(slotStart, historyDays, previousSlot),
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    MinimumWidth = MinTimeColumnWidth,
                    SlotStart = slotStart,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = Color.White,
                        SelectionBackColor = Color.FromArgb(245, 248, 255),
                        SelectionForeColor = Color.Black
                    }
                };
                _grid.Columns.Add(timeColumn);
                ApplyTimeColumnHeaderStyle(timeColumn, isDateHeader);
            }

            ApplyHorizontalStretch(historyDays);
            HighlightCurrentHourColumn(hourSlots, timelineStart, timelineEnd);

            List<string> distinctJobs = executions
                .Select(j => j.JobName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctJobs.Count == 0)
            {
                distinctJobs.Add("(nincs adat)");
            }

            foreach (string jobName in distinctJobs)
            {
                int rowIndex = _grid.Rows.Add();
                DataGridViewRow row = _grid.Rows[rowIndex];
                row.Height = 26;
                row.Cells[0].Value = jobName;

                if (jobName == "(nincs adat)")
                    continue;

                List<JobExecution> jobRuns = executions
                    .Where(j => string.Equals(j.JobName, jobName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                for (int columnIndex = 0; columnIndex < hourSlots.Count; columnIndex++)
                {
                    DateTime slotStart = hourSlots[columnIndex];
                    DateTime slotEnd = slotStart.AddHours(1);

                    List<JobExecution> overlapping = jobRuns
                        .Where(run => run.StartTime < slotEnd && run.FinishTime > slotStart)
                        .ToList();

                    if (overlapping.Count == 0)
                    {
                        row.Cells[columnIndex + 1].ToolTipText = string.Empty;
                        continue;
                    }

                    TimelineHourCell cell = (TimelineHourCell)row.Cells[columnIndex + 1];
                    cell.Value = string.Empty;
                    cell.Segments = TimelineCellSegmentCalculator.BuildSegments(slotStart, slotEnd, overlapping);
                    cell.ToolTipText = JobExecutionToolTipFormatter.BuildCellToolTip(overlapping);
                }
            }

            EnableDoubleBuffering(_grid);
            _grid.Invalidate();

            _grid.ResumeLayout();
        }

        private void ApplyHorizontalStretch(int historyDays)
        {
            if (_grid.Columns.Count <= 1)
                return;

            int minTimeColumnWidth = historyDays == 7 ? 40 : MinTimeColumnWidth;

            _grid.Columns[0].Width = JobColumnWidth;

            int timeColumnCount = _grid.Columns.Count - 1;
            int availableWidth = Math.Max(
                0,
                _grid.ClientSize.Width
                - JobColumnWidth
                - (_grid.DisplayedRowCount(false) < _grid.RowCount
                    ? SystemInformation.VerticalScrollBarWidth
                    : 0));

            int stretchedWidth = Math.Max(
                minTimeColumnWidth,
                availableWidth / timeColumnCount);

            for (int i = 1; i < _grid.Columns.Count; i++)
                _grid.Columns[i].Width = stretchedWidth;

            int totalWidth = JobColumnWidth + (stretchedWidth * timeColumnCount);
            _grid.ScrollBars = totalWidth > _grid.ClientSize.Width
                ? ScrollBars.Both
                : ScrollBars.Vertical;
        }

        private static string FormatTimeColumnHeader(
            DateTime slotStart,
            int historyDays,
            DateTime? previousSlot)
        {
            if (historyDays == 1)
            {
                bool dayChanged = previousSlot == null || previousSlot.Value.Date != slotStart.Date;

                if (dayChanged)
                    return $"{slotStart:HH:mm}\n{slotStart:MM.dd.}";

                return slotStart.ToString("HH:mm");
            }

            if (slotStart.Hour == 0)
                return slotStart.ToString("MM.dd.");

            return slotStart.ToString("HH:mm");
        }

        private static bool IsDateHeaderColumn(
            DateTime slotStart,
            int historyDays,
            DateTime? previousSlot)
        {
            if (historyDays == 7)
                return slotStart.Hour == 0;

            return previousSlot == null || previousSlot.Value.Date != slotStart.Date;
        }

        private static void ApplyTimeColumnHeaderStyle(DataGridViewColumn column, bool isDateHeader)
        {
            DataGridViewCellStyle style = column.HeaderCell.Style;

            if (isDateHeader)
            {
                style.BackColor = HeaderDateBackColor;
                style.ForeColor = HeaderDateForeColor;
                style.Font = HeaderDateFont;
                return;
            }

            style.BackColor = HeaderHourBackColor;
            style.ForeColor = HeaderHourForeColor;
            style.Font = HeaderHourFont;
        }

        private static (DateTime Start, DateTime End) GetTimelineRange(int historyDays)
        {
            DateTime timelineStart = historyDays == 1
                ? DateTime.Today.AddDays(-1).AddHours(20)
                : DateTime.Today.AddDays(-7);

            return (timelineStart, DateTime.Now);
        }

        private static List<DateTime> BuildHourSlots(DateTime timelineStart, DateTime timelineEnd)
        {
            List<DateTime> slots = new List<DateTime>();

            DateTime hour = new DateTime(
                timelineStart.Year,
                timelineStart.Month,
                timelineStart.Day,
                timelineStart.Hour,
                0,
                0);

            while (hour <= timelineEnd)
            {
                slots.Add(hour);
                hour = hour.AddHours(1);
            }

            return slots;
        }

        private void HighlightCurrentHourColumn(
            IReadOnlyList<DateTime> hourSlots,
            DateTime timelineStart,
            DateTime timelineEnd)
        {
            DateTime now = DateTime.Now;
            if (now < timelineStart || now > timelineEnd)
                return;

            for (int i = 0; i < hourSlots.Count; i++)
            {
                DateTime slotStart = hourSlots[i];
                DateTime slotEnd = slotStart.AddHours(1);
                if (now >= slotStart && now < slotEnd)
                {
                    DataGridViewColumn column = _grid.Columns[i + 1];
                    column.HeaderCell.Style.BackColor = Color.MistyRose;
                    column.HeaderCell.Style.ForeColor = Color.DarkRed;
                    column.HeaderCell.Style.Font = IsDateHeaderColumn(
                        slotStart,
                        _historyDays,
                        i > 0 ? hourSlots[i - 1] : null)
                        ? HeaderDateFont
                        : HeaderHourFont;
                    break;
                }
            }
        }

        private static void EnableDoubleBuffering(DataGridView grid)
        {
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.SetProperty,
                null,
                grid,
                new object[] { true });
        }
    }
}
