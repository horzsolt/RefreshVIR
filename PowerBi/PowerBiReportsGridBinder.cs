namespace RefreshVIR
{
    internal static class PowerBiReportsGridBinder
    {
        internal static void Bind(DataGridView grid, IReadOnlyList<PowerBiReportInfo> reports)
        {
            grid.DataSource = null;
            grid.Columns.Clear();
            grid.AutoGenerateColumns = false;

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.ReportName),
                HeaderText = "Riport neve",
                FillWeight = 180
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.DataSourceDisplay),
                HeaderText = "Adatforrás",
                FillWeight = 140
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.ReportType),
                HeaderText = "Típus",
                FillWeight = 110
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.LastUploadDisplay),
                HeaderText = "Utolsó feltöltés",
                FillWeight = 120
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.LastRefreshDisplay),
                HeaderText = "Utolsó adatfrissítés",
                FillWeight = 120
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.NextRefreshDisplay),
                HeaderText = "Következő adatfrissítés",
                FillWeight = 120
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.ScheduleDisplay),
                HeaderText = "Ütemezés",
                FillWeight = 90
            });

            grid.DataSource = reports.ToList();
        }

        internal static void ApplyRowStyles(DataGridView grid)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is not PowerBiReportInfo report)
                    continue;

                if (report.HasEmbeddedReportData)
                {
                    row.DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
                    row.DefaultCellStyle.SelectionBackColor = Color.Khaki;
                    row.DefaultCellStyle.SelectionForeColor = Color.Black;
                }
                else if (report.RefreshDisabled)
                {
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
                    row.DefaultCellStyle.SelectionBackColor = Color.LightCoral;
                    row.DefaultCellStyle.SelectionForeColor = Color.Black;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = grid.DefaultCellStyle.BackColor;
                    row.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
                    row.DefaultCellStyle.SelectionForeColor = Color.Black;
                }
            }
        }
    }
}
