namespace RefreshVIR
{
    internal static class DataGridViewErrorHandler
    {
        internal static void Attach(DataGridView grid, IWin32Window? owner, string contextName)
        {
            grid.DataError += (_, e) => HandleDataError(owner, contextName, grid, e);
        }

        private static void HandleDataError(
            IWin32Window? owner,
            string contextName,
            DataGridView grid,
            DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            e.Cancel = true;

            string cellInfo = e.RowIndex >= 0 && e.ColumnIndex >= 0
                ? $"Sor: {e.RowIndex + 1}, oszlop: {grid.Columns[e.ColumnIndex].HeaderText}"
                : "Ismeretlen cella";

            string summary =
                $"Adat megjelenítési hiba ({contextName}).{Environment.NewLine}{cellInfo}";

            Exception exception = e.Exception
                ?? new InvalidOperationException(summary);

            ErrorDialog.ShowErrorOnce(
                owner ?? grid.FindForm(),
                "Adat megjelenítési hiba",
                summary,
                exception);
        }
    }
}
