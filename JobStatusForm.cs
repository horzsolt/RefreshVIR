using Microsoft.Data.SqlClient;
using System;
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
            this.Text = "SQL Server Agent Job Monitor";

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

            grid.AllowUserToAddRows = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            grid.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font(grid.Font, System.Drawing.FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            Controls.Add(grid);
            Controls.Add(closeButton);

            Load += JobStatusForm_Load;
        }

        private void JobStatusForm_Load(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                DataTable dt = GetJobDetails();
                grid.DataSource = dt;
                grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
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

        private DataTable GetJobDetails()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Frissítés neve");
            dt.Columns.Add("Utoljára futott");
            dt.Columns.Add("Következő futás");
            dt.Columns.Add("Utolsó futás időtartama (min)");
            dt.Columns.Add("Átlagos futási idő (min)");
            dt.Columns.Add("Utolsó futás státusza");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                foreach (var kvp in jobs)
                {
                    string jobName = kvp.Key;
                    string displayName = string.IsNullOrWhiteSpace(kvp.Value) ? jobName : kvp.Value;

                    string nextSchedule = "";
                    string lastStatus = "";
                    double avgRuntime = 0;
                    string lastExecutionTime = "";
                    double lastExecutionDuration = 0;

                    // --- Get schedule + last run ---
                    using (SqlCommand cmd = new SqlCommand(@"
                SELECT 
                    j.name,
                    ja.next_scheduled_run_date,
                    h.run_status,
                    h.run_date,
                    h.run_time,
                    h.run_duration
                FROM msdb.dbo.sysjobs j
                LEFT JOIN msdb.dbo.sysjobactivity ja 
                    ON j.job_id = ja.job_id
                   AND ja.session_id = (SELECT MAX(session_id) FROM msdb.dbo.sysjobactivity)
                OUTER APPLY (
                    SELECT TOP 1 * 
                    FROM msdb.dbo.sysjobhistory h
                    WHERE h.job_id = j.job_id AND h.step_id = 0
                    ORDER BY h.run_date DESC, h.run_time DESC
                ) h
                WHERE j.name = @JobName", conn))
                    {
                        cmd.Parameters.AddWithValue("@JobName", jobName);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Next schedule
                                if (reader["next_scheduled_run_date"] != DBNull.Value)
                                {
                                    DateTime next = (DateTime)reader["next_scheduled_run_date"];
                                    nextSchedule = next.ToString("yyyy-MM-dd HH:mm");
                                }

                                // Last status
                                if (reader["run_status"] != DBNull.Value)
                                {
                                    int status = Convert.ToInt32(reader["run_status"]);
                                    lastStatus = status switch
                                    {
                                        0 => "Failed",
                                        1 => "Succeeded",
                                        2 => "Retry",
                                        3 => "Canceled",
                                        4 => "In Progress",
                                        _ => "Unknown"
                                    };
                                }

                                // Last execution time
                                if (reader["run_date"] != DBNull.Value && reader["run_time"] != DBNull.Value)
                                {
                                    int runDate = Convert.ToInt32(reader["run_date"]);
                                    int runTime = Convert.ToInt32(reader["run_time"]);
                                    string runDateStr = runDate.ToString();
                                    string runTimeStr = runTime.ToString("D6");
                                    DateTime lastExec = DateTime.ParseExact(runDateStr + runTimeStr, "yyyyMMddHHmmss", null);
                                    lastExecutionTime = lastExec.ToString("yyyy-MM-dd HH:mm:ss");
                                }

                                // Last execution duration
                                if (reader["run_duration"] != DBNull.Value)
                                {
                                    int dur = Convert.ToInt32(reader["run_duration"]);
                                    int hh = dur / 10000;
                                    int mm = (dur % 10000) / 100;
                                    int ss = dur % 100;
                                    int totalSeconds = hh * 3600 + mm * 60 + ss;
                                    lastExecutionDuration = totalSeconds; // store total seconds
                                }
                            }
                        }
                    }

                    // --- Get average runtime (last 7 days) ---
                    using (SqlCommand cmd = new SqlCommand(@"
                SELECT AVG(run_duration)
                FROM msdb.dbo.sysjobs j
                JOIN msdb.dbo.sysjobhistory h ON j.job_id = h.job_id
                WHERE j.name = @JobName
                  AND h.step_id = 0
                  AND msdb.dbo.agent_datetime(h.run_date, h.run_time) > DATEADD(DAY, -7, GETDATE())", conn))
                    {
                        cmd.Parameters.AddWithValue("@JobName", jobName);

                        object result = cmd.ExecuteScalar();
                        if (result != DBNull.Value && result != null)
                        {
                            double avgDur = Convert.ToDouble(result);
                            int hh = (int)(avgDur / 10000);
                            int mm = (int)((avgDur % 10000) / 100);
                            int ss = (int)(avgDur % 100);
                            int totalSeconds = hh * 3600 + mm * 60 + ss;
                            avgRuntime = totalSeconds;
                        }
                    }

                    // --- Add row in the correct column order ---
                    dt.Rows.Add(displayName, lastExecutionTime, nextSchedule,
                                TimeSpan.FromSeconds(lastExecutionDuration).ToString(@"mm\:ss"),
                                TimeSpan.FromSeconds(avgRuntime).ToString(@"mm\:ss"), lastStatus);
                }
            }

            return dt;
        }
    }
}
