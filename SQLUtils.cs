using Microsoft.Data.SqlClient;
using System.Data;


namespace RefreshVIR
{
    public static class SQLUtils
    {
        public static async Task ExecuteJobAsync(string jobName, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentException("Job name cannot be null or empty.", nameof(jobName));

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("msdb.dbo.sp_start_job", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@job_name", jobName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static bool IsJobRunning(string jobName, string connectionString)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"
            SELECT 
                ja.start_execution_date,
                ja.stop_execution_date
            FROM msdb.dbo.sysjobs j
            JOIN msdb.dbo.sysjobactivity ja ON j.job_id = ja.job_id
            WHERE j.name = @JobName
              AND ja.start_execution_date IS NOT NULL
              AND ja.stop_execution_date IS NULL
              AND ja.session_id = (SELECT MAX(session_id) FROM msdb.dbo.sysjobactivity)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@JobName", jobName);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // If a record exists with start but no stop date → job is running
                            return true;
                        }
                    }
                }
            }

            return false;
        }


        public static DataTable GetStoredProcExecutionDetails(string jobName, string connectionString)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Utoljára futtatva");
            dt.Columns.Add("Következő futtatás");
            dt.Columns.Add("Utolsó futási idő");
            dt.Columns.Add("Átlagos futási idő");
            dt.Columns.Add("Utolsó futás státusza");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // --- Get next scheduled run ---
                DateTime? nextSchedule = null;
                using (SqlCommand cmdNext = new SqlCommand(@"
            SELECT TOP 1 ja.next_scheduled_run_date
            FROM msdb.dbo.sysjobs j
            JOIN msdb.dbo.sysjobactivity ja ON j.job_id = ja.job_id
            WHERE j.name = @JobName
              AND ja.session_id = (SELECT MAX(session_id) FROM msdb.dbo.sysjobactivity)", conn))
                {
                    cmdNext.Parameters.AddWithValue("@JobName", jobName);
                    object result = cmdNext.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        nextSchedule = (DateTime)result;
                }

                // --- Get average runtime (last 7 days) ---
                double avgSeconds = 0;
                using (SqlCommand cmdAvg = new SqlCommand(@"
            SELECT AVG(run_duration)
            FROM msdb.dbo.sysjobs j
            JOIN msdb.dbo.sysjobhistory h ON j.job_id = h.job_id
            WHERE j.name = @JobName
              AND h.step_id = 0
              AND msdb.dbo.agent_datetime(h.run_date, h.run_time) > DATEADD(DAY, -7, GETDATE())", conn))
                {
                    cmdAvg.Parameters.AddWithValue("@JobName", jobName);
                    object result = cmdAvg.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        avgSeconds = ConvertDurationToSeconds(Convert.ToInt32(result));
                }

                // --- Get all executions from the last 7 days ---
                using (SqlCommand cmdHist = new SqlCommand(@"
            SELECT TOP 100
                msdb.dbo.agent_datetime(h.run_date, h.run_time) AS RunTime,
                h.run_duration,
                h.run_status
            FROM msdb.dbo.sysjobs j
            JOIN msdb.dbo.sysjobhistory h ON j.job_id = h.job_id
            WHERE j.name = @JobName
              AND h.step_id = 0
              AND msdb.dbo.agent_datetime(h.run_date, h.run_time) > DATEADD(DAY, -7, GETDATE())
            ORDER BY h.run_date DESC, h.run_time DESC", conn))
                {
                    cmdHist.Parameters.AddWithValue("@JobName", jobName);

                    using (SqlDataReader reader = cmdHist.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string execTime = reader[0] != DBNull.Value ? ((DateTime)reader[0]).ToString("yyyy-MM-dd HH:mm:ss") : "";
                            string nextSch = nextSchedule.HasValue ? nextSchedule.Value.ToString("yyyy-MM-dd HH:mm") : "";

                            int lastDur = reader[1] != DBNull.Value ? Convert.ToInt32(reader[1]) : 0;
                            double lastSeconds = ConvertDurationToSeconds(lastDur);

                            string lastStatus = "Unknown";
                            if (reader[2] != DBNull.Value)
                            {
                                int status = Convert.ToInt32(reader[2]);
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

                            dt.Rows.Add(
                                execTime,
                                nextSch,
                                TimeSpan.FromSeconds(lastSeconds).ToString(@"mm\:ss"),
                                TimeSpan.FromSeconds(avgSeconds).ToString(@"mm\:ss"),
                                lastStatus
                            );
                        }
                    }
                }
            }

            return dt;
        }

        private static double ConvertDurationToSeconds(int runDuration)
        {
            int hh = runDuration / 10000;
            int mm = (runDuration % 10000) / 100;
            int ss = runDuration % 100;
            return hh * 3600 + mm * 60 + ss;
        }
    }
}
