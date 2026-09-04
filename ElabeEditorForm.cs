using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace RefreshVIR
{
    public sealed class ElabeEditorForm : Form
    {
        private const string TableName = "dbo.t_e_elabe_2026";
        private const string SelectSql =
            "SELECT ID, BIZHO, TERMEK_KOD, TERMEK_NEV, E_ELABE FROM " + TableName + " ORDER BY ID";

        private enum Mode { Browse, Edit, Insert }

        private readonly string _connectionString;
        private readonly BindingSource _binding = new();
        private readonly DataGridView _grid = new();
        private readonly Button _btnNew = new() { Text = "Új", Width = 100, Height = 28 };
        private readonly Button _btnEdit = new() { Text = "Módosít", Width = 100, Height = 28 };
        private readonly Button _btnDelete = new() { Text = "Töröl", Width = 100, Height = 28 };
        private readonly Button _btnSave = new() { Text = "Rögzít", Width = 100, Height = 28, Visible = false, CausesValidation = false };
        private readonly Button _btnCancel = new() { Text = "Elvet", Width = 100, Height = 28, Visible = false, CausesValidation = false };

        private static readonly Color EditRowBackColor = Color.FromArgb(255, 224, 130);
        private static readonly Color EditRowSelectionBackColor = Color.FromArgb(255, 179, 0);

        private DataTable _table = new();
        private Mode _mode = Mode.Browse;
        private int _lockedRowIndex = -1;
        private DataRow? _lockedDataRow;
        private bool _allowRowLeave;
        private bool _syncingSelection;
        private bool _invalidValueHandled;
        private bool _cellValidationFailed;
        private bool _suppressCellValidating;

        public ElabeEditorForm(string connectionString)
        {
            _connectionString = connectionString;
            ApplicationBrand.Apply(this);
            BuildUi();
            Load += async (_, _) => await LoadDataAsync();
            FormClosing += OnFormClosing;
            FormClosed += (_, _) => SQLUtils.LogAction("ELABE editor bezárva");
        }

        private void BuildUi()
        {
            Text = "ELABE editor";
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoValidate = AutoValidate.EnableAllowFocusChange;
            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    RequestClose();
                    e.Handled = true;
                }
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 6, 8, 6),
                WrapContents = false,
                CausesValidation = false
            };
            _btnNew.Click += (_, _) => StartInsert();
            _btnEdit.Click += (_, _) => StartEdit();
            _btnDelete.Click += async (_, _) => await DeleteCurrentAsync();
            _btnSave.MouseDown += async (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    await SaveAsync();
            };
            _btnCancel.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    CancelEdit();
            };
            toolbar.Controls.AddRange([_btnNew, _btnEdit, _btnDelete, _btnSave, _btnCancel]);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.RowHeadersVisible = false;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
            _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            _grid.DataSource = _binding;
            _grid.DataBindingComplete += (_, _) => ConfigureColumns();
            _grid.CellBeginEdit += Grid_CellBeginEdit;
            _grid.RowValidating += Grid_RowValidating;
            _grid.CurrentCellChanged += Grid_CurrentCellChanged;
            _grid.KeyDown += Grid_KeyDown;
            _grid.CellValidating += Grid_CellValidating;
            _grid.CellParsing += Grid_CellParsing;
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.DataError += Grid_DataError;

            var closeButton = new Button
            {
                Text = "<< Vissza",
                Dock = DockStyle.Bottom,
                Height = 40,
                CausesValidation = false
            };
            closeButton.Click += (_, _) => RequestClose();

            Controls.Add(_grid);
            Controls.Add(closeButton);
            Controls.Add(toolbar);
        }

        protected override void WndProc(ref Message m)
        {
            const int WmClose = 0x0010;
            if (m.Msg == WmClose)
                _allowRowLeave = true;

            base.WndProc(ref m);
        }

        private async Task LoadDataAsync()
        {
            UseWaitCursor = true;
            SetBusy(true);
            try
            {
                _table = await Task.Run(LoadTable);
                _binding.DataSource = _table;
            }
            catch (Exception ex)
            {
                ErrorDialog.ShowError(this, "Hiba", "Az ELABE tábla betöltése sikertelen:\n" + ex.Message, ex);
            }
            finally
            {
                UseWaitCursor = false;
                SetBusy(_table.Columns.Count == 0);
            }
        }

        private DataTable LoadTable()
        {
            using var connection = new SqlConnection(_connectionString);
            using var adapter = new SqlDataAdapter(SelectSql, connection);
            var table = new DataTable();
            adapter.Fill(table);
            if (table.Columns["ID"] is { } id)
            {
                id.AutoIncrement = false;
                id.ReadOnly = false;
                id.AllowDBNull = true;
            }
            return table;
        }

        private void ConfigureColumns()
        {
            foreach (DataGridViewColumn column in _grid.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;

            if (_grid.Columns["ID"] is { } idColumn)
            {
                idColumn.ReadOnly = true;
                idColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                idColumn.Width = 70;
            }

            if (_grid.Columns["BIZHO"] is DataGridViewTextBoxColumn bizhoColumn)
            {
                bizhoColumn.MaxInputLength = 6;
                bizhoColumn.DefaultCellStyle.Format = string.Empty;
                bizhoColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void StartEdit()
        {
            if (!TryGetCurrentRow(out _))
            {
                MessageBox.Show(this, "Nincs kijelölt rekord.", "Figyelmeztetés",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            PrepareGridForEditing();
            EnterMode(Mode.Edit);
        }

        private void StartInsert()
        {
            PrepareGridForEditing();
            DataRow row = _table.NewRow();
            _table.Rows.Add(row);

            int rowIndex = FindRowIndex(row);
            if (rowIndex < 0)
                return;

            _grid.CurrentCell = _grid.Rows[rowIndex].Cells[FirstEditableColumnIndex()];
            try
            {
                _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            }
            catch (InvalidOperationException)
            {
            }

            EnterMode(Mode.Insert);
        }

        private void PrepareGridForEditing()
        {
            _grid.ReadOnly = false;
            if (_grid.Columns["ID"] is { } idColumn)
                idColumn.ReadOnly = true;
        }

        private void EnterMode(Mode mode)
        {
            _mode = mode;
            _lockedRowIndex = _grid.CurrentRow?.Index ?? -1;
            _lockedDataRow = GetDataRow(_lockedRowIndex);
            SetToolbarEditing(true);
            ApplyEditRowHighlight();

            int rowIndex = _lockedRowIndex;
            int columnIndex = FirstEditableColumnIndex();
            BeginInvoke(() => FocusCellForEdit(rowIndex, columnIndex));
        }

        private void CancelEdit()
        {
            _allowRowLeave = true;
            try
            {
                if (_grid.IsCurrentCellInEditMode)
                    _grid.CancelEdit();
                _binding.CancelEdit();
                if (TryGetCurrentRow(out DataRow row) && row.RowState != DataRowState.Unchanged)
                    row.RejectChanges();

                ExitMode(commitCell: false);
            }
            finally
            {
                _allowRowLeave = false;
            }
        }

        private void ExitMode(bool commitCell = true)
        {
            _mode = Mode.Browse;
            _lockedRowIndex = -1;
            _lockedDataRow = null;
            if (commitCell && _grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            SetToolbarEditing(false);
            _grid.Invalidate();
        }

        private async Task SaveAsync()
        {
            if (_mode == Mode.Browse)
                return;

            _allowRowLeave = true;
            try
            {
                if (TryRejectInvalidCurrentEditor())
                    return;

                bool validationFailed = _cellValidationFailed;
                if (!validationFailed && _grid.IsCurrentCellInEditMode)
                {
                    if (!_grid.EndEdit())
                        validationFailed = true;
                    validationFailed = validationFailed || _cellValidationFailed;
                }

                if (validationFailed)
                    return;

                _binding.EndEdit();
                if (_cellValidationFailed || !TryGetCurrentRow(out DataRow row))
                    return;

                if (TryRejectInvalidBizhoOnRow(row))
                    return;

                if (TryRejectInvalidRequiredOnRow(row))
                    return;

                UseWaitCursor = true;
                try
                {
                    if (_mode == Mode.Insert)
                        row["ID"] = await Task.Run(() => InsertRow(row));
                    else
                        await Task.Run(() => UpdateRow(row));

                    row.AcceptChanges();
                    SQLUtils.LogAction(_mode == Mode.Insert
                        ? $"ELABE rekord beszúrva (ID={row["ID"]})"
                        : $"ELABE rekord módosítva (ID={row["ID"]})");
                    ExitMode();
                }
                catch (Exception ex)
                {
                    ErrorDialog.ShowError(this, "Hiba", "A rekord rögzítése sikertelen:\n" + ex.Message, ex);
                }
                finally
                {
                    UseWaitCursor = false;
                }
            }
            finally
            {
                if (_mode != Mode.Browse)
                    _allowRowLeave = false;
                _cellValidationFailed = false;
            }
        }

        private async Task DeleteCurrentAsync()
        {
            if (!TryGetCurrentRow(out DataRow row) || row["ID"] is not int id)
            {
                MessageBox.Show(this, "Nincs kijelölt rekord.", "Figyelmeztetés",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"Biztosan törli a(z) {id} azonosítójú rekordot?",
                "Törlés megerősítése",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            UseWaitCursor = true;
            try
            {
                await Task.Run(() => DeleteRow(id));
                row.Delete();
                row.AcceptChanges();
                SQLUtils.LogAction($"ELABE rekord törölve (ID={id})");
            }
            catch (Exception ex)
            {
                ErrorDialog.ShowError(this, "Hiba", "A rekord törlése sikertelen:\n" + ex.Message, ex);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private int InsertRow(DataRow row)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                $@"INSERT INTO {TableName} (BIZHO, TERMEK_KOD, TERMEK_NEV, E_ELABE)
                   OUTPUT INSERTED.ID
                   VALUES (@BIZHO, @TERMEK_KOD, @TERMEK_NEV, @E_ELABE)",
                connection);
            AddDataParameters(command, row);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private void UpdateRow(DataRow row)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                $@"UPDATE {TableName}
                   SET BIZHO = @BIZHO, TERMEK_KOD = @TERMEK_KOD,
                       TERMEK_NEV = @TERMEK_NEV, E_ELABE = @E_ELABE
                   WHERE ID = @ID",
                connection);
            AddDataParameters(command, row);
            command.Parameters.AddWithValue("@ID", row["ID"]);
            command.ExecuteNonQuery();
        }

        private void DeleteRow(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand($"DELETE FROM {TableName} WHERE ID = @ID", connection);
            command.Parameters.AddWithValue("@ID", id);
            command.ExecuteNonQuery();
        }

        private static void AddDataParameters(SqlCommand command, DataRow row)
        {
            command.Parameters.AddWithValue("@BIZHO", row["BIZHO"]);
            command.Parameters.AddWithValue("@TERMEK_KOD", row["TERMEK_KOD"]);
            command.Parameters.AddWithValue("@TERMEK_NEV", row["TERMEK_NEV"]);
            command.Parameters.AddWithValue("@E_ELABE", row["E_ELABE"]);
        }

        private void RequestClose()
        {
            if (_mode != Mode.Browse)
            {
                if (!ConfirmDiscard())
                {
                    RestoreEditFocus();
                    return;
                }

                CancelEdit();
            }

            _allowRowLeave = true;
            Close();
        }

        private bool ConfirmDiscard()
        {
            string message = _mode == Mode.Insert
                ? "Új rekord felvitele folyamatban. Elveti a beszúrást és bezárja az ablakot?"
                : "Szerkesztés folyamatban. Elveti a módosításokat és bezárja az ablakot?";

            return MessageBox.Show(
                this, message, "Megerősítés",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
                == DialogResult.Yes;
        }

        private void RestoreEditFocus()
        {
            BeginInvoke(() =>
            {
                if (IsDisposed || _mode == Mode.Browse)
                    return;

                int rowIndex = FindRowIndex(_lockedDataRow);
                if (rowIndex < 0)
                    return;

                _lockedRowIndex = rowIndex;
                _grid.Focus();
                DataGridViewCell cell = _grid.Rows[rowIndex].Cells[FirstEditableColumnIndex()];
                if (!ReferenceEquals(_grid.CurrentCell, cell))
                    _grid.CurrentCell = cell;

                try
                {
                    _grid.FirstDisplayedScrollingRowIndex = rowIndex;
                }
                catch (InvalidOperationException)
                {
                }

                _grid.BeginEdit(true);
            });
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_mode == Mode.Browse)
                return;

            if (!ConfirmDiscard())
            {
                _allowRowLeave = false;
                e.Cancel = true;
                RestoreEditFocus();
                return;
            }

            CancelEdit();
        }

        private void Grid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (_mode == Mode.Browse)
                return;
            if (GetDataRow(e.RowIndex) != _lockedDataRow)
                e.Cancel = true;
        }

        private void Grid_RowValidating(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (_mode == Mode.Browse || _allowRowLeave)
                return;

            Point client = _grid.PointToClient(Control.MousePosition);
            DataGridView.HitTestInfo hit = _grid.HitTest(client.X, client.Y);
            bool clickOnAnotherRow =
                hit.Type == DataGridViewHitTestType.Cell && GetDataRow(hit.RowIndex) != _lockedDataRow;
            bool clickOutsideGrid = !_grid.ClientRectangle.Contains(client);

            if (clickOnAnotherRow || !clickOutsideGrid)
                e.Cancel = true;
        }

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_mode == Mode.Browse)
                return;

            if (e.KeyCode is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown
                or Keys.Home or Keys.End)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void Grid_CurrentCellChanged(object? sender, EventArgs e)
        {
            if (_syncingSelection || _allowRowLeave || _mode == Mode.Browse)
                return;
            if (GetDataRow(_grid.CurrentCell?.RowIndex ?? -1) == _lockedDataRow)
                return;

            BeginInvoke(RestoreLockedCell);
        }

        private void RestoreLockedCell()
        {
            if (_syncingSelection || _allowRowLeave || _mode == Mode.Browse || IsDisposed)
                return;

            int rowIndex = FindRowIndex(_lockedDataRow);
            if (rowIndex < 0)
                return;
            if (_grid.CurrentCell?.RowIndex == rowIndex)
                return;

            _lockedRowIndex = rowIndex;
            _syncingSelection = true;
            try
            {
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells[FirstEditableColumnIndex()];
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private DataRow? GetDataRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return null;
            return (_grid.Rows[rowIndex].DataBoundItem as DataRowView)?.Row;
        }

        private int FindRowIndex(DataRow? row)
        {
            if (row == null)
                return -1;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (GetDataRow(i) == row)
                    return i;
            }

            return -1;
        }

        private DataColumn? GetTableColumn(int gridColumnIndex)
        {
            if (gridColumnIndex < 0 || gridColumnIndex >= _grid.Columns.Count)
                return null;

            string name = _grid.Columns[gridColumnIndex].DataPropertyName;
            if (string.IsNullOrEmpty(name))
                name = _grid.Columns[gridColumnIndex].Name;

            return _table.Columns.Contains(name) ? _table.Columns[name] : null;
        }

        private string GetBoundColumnName(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
                return string.Empty;

            string name = _grid.Columns[columnIndex].DataPropertyName;
            return string.IsNullOrEmpty(name) ? _grid.Columns[columnIndex].Name : name;
        }

        private bool IsColumn(int columnIndex, string columnName)
        {
            return string.Equals(GetBoundColumnName(columnIndex), columnName, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsBizhoColumn(int columnIndex) => IsColumn(columnIndex, "BIZHO");

        private bool IsTermekKodColumn(int columnIndex) => IsColumn(columnIndex, "TERMEK_KOD");

        private bool IsElabeColumn(int columnIndex) => IsColumn(columnIndex, "E_ELABE");

        private int IndexOfColumn(string columnName)
        {
            foreach (DataGridViewColumn column in _grid.Columns)
            {
                string name = string.IsNullOrEmpty(column.DataPropertyName) ? column.Name : column.DataPropertyName;
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return column.Index;
            }

            return -1;
        }

        private string ColumnHeader(int columnIndex)
        {
            if (columnIndex >= 0 && columnIndex < _grid.Columns.Count)
                return _grid.Columns[columnIndex].HeaderText;

            return "mező";
        }

        private string RequiredFieldMessage(int columnIndex)
        {
            return $"A(z) {ColumnHeader(columnIndex)} mező kitöltése kötelező.";
        }

        private string NumberFieldMessage(int columnIndex)
        {
            return $"A(z) {ColumnHeader(columnIndex)} mező csak számot fogad el.";
        }

        private static bool TryParseBizho(string text, out int value)
        {
            value = 0;
            text = text.Trim();
            if (text.Length != 6)
                return false;

            foreach (char c in text)
            {
                if (c is < '0' or > '9')
                    return false;
            }

            int month = (text[4] - '0') * 10 + (text[5] - '0');
            if (month is < 1 or > 12)
                return false;

            value = int.Parse(text, CultureInfo.InvariantCulture);
            return true;
        }

        private static string BizhoToUnpaddedText(object? raw)
        {
            if (raw == null || raw is DBNull)
                return string.Empty;

            if (raw is int intValue)
                return intValue.ToString(CultureInfo.InvariantCulture);

            return Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        private static bool IsBizhoValueValid(object? raw)
        {
            string text = BizhoToUnpaddedText(raw);
            return string.IsNullOrEmpty(text) || TryParseBizho(text, out _);
        }

        private bool TryRejectInvalidCurrentEditor()
        {
            if (!_grid.IsCurrentCellInEditMode || _grid.CurrentCell == null)
                return false;

            int columnIndex = _grid.CurrentCell.ColumnIndex;
            int rowIndex = _grid.CurrentCell.RowIndex;
            string text = _grid.EditingControl?.Text ?? string.Empty;

            if (IsBizhoColumn(columnIndex))
            {
                if (string.IsNullOrWhiteSpace(text) || TryParseBizho(text, out _))
                    return false;

                RejectInvalidCell(rowIndex, columnIndex, BizhoInvalidMessage);
                return true;
            }

            if (IsTermekKodColumn(columnIndex))
            {
                if (!string.IsNullOrWhiteSpace(text))
                    return false;

                RejectRequiredCell(rowIndex, columnIndex, RequiredFieldMessage(columnIndex));
                return true;
            }

            if (IsElabeColumn(columnIndex))
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    RejectRequiredCell(rowIndex, columnIndex, RequiredFieldMessage(columnIndex));
                    return true;
                }

                DataColumn? column = GetTableColumn(columnIndex);
                if (column != null && TryConvertValue(text, column.DataType, out _))
                    return false;

                RejectInvalidCell(rowIndex, columnIndex, NumberFieldMessage(columnIndex));
                return true;
            }

            return false;
        }

        private bool TryRejectInvalidBizhoOnRow(DataRow row)
        {
            if (IsBizhoValueValid(row["BIZHO"]))
                return false;

            int rowIndex = FindRowIndex(row);
            if (rowIndex < 0)
                rowIndex = _grid.CurrentCell?.RowIndex ?? 0;

            RejectInvalidCell(rowIndex, FirstEditableColumnIndex(), BizhoInvalidMessage);
            return true;
        }

        private bool TryRejectInvalidRequiredOnRow(DataRow row)
        {
            int rowIndex = FindRowIndex(row);
            if (rowIndex < 0)
                rowIndex = _grid.CurrentCell?.RowIndex ?? 0;

            int termekKodIndex = IndexOfColumn("TERMEK_KOD");
            if (termekKodIndex >= 0 && IsMissingValue(row["TERMEK_KOD"]))
            {
                RejectRequiredCell(rowIndex, termekKodIndex, RequiredFieldMessage(termekKodIndex));
                return true;
            }

            int elabeIndex = IndexOfColumn("E_ELABE");
            if (elabeIndex < 0)
                return false;

            if (IsMissingValue(row["E_ELABE"]))
            {
                RejectRequiredCell(rowIndex, elabeIndex, RequiredFieldMessage(elabeIndex));
                return true;
            }

            if (row["E_ELABE"] is float or double or decimal or int or long or short)
                return false;

            RejectInvalidCell(rowIndex, elabeIndex, NumberFieldMessage(elabeIndex));
            return true;
        }

        private static bool IsMissingValue(object? raw)
        {
            if (raw == null || raw is DBNull)
                return true;

            return string.IsNullOrWhiteSpace(Convert.ToString(raw, CultureInfo.CurrentCulture));
        }

        private const string BizhoInvalidMessage =
            "A BIZHO mező formátuma ÉÉÉÉHH: pontosan 6 számjegy (év: 0000–9999, hónap: 01–12).";

        private static string FriendlyTypeName(Type type)
        {
            if (type == typeof(int) || type == typeof(long) || type == typeof(short))
                return "egész számot";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "számot";
            if (type == typeof(DateTime))
                return "dátumot";
            return "érvényes adatot";
        }

        private static bool TryConvertValue(string text, Type type, out object value)
        {
            try
            {
                if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int intValue))
                {
                    value = intValue;
                    return true;
                }

                if (type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double doubleValue))
                {
                    value = doubleValue;
                    return true;
                }

                if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out float floatValue))
                {
                    value = floatValue;
                    return true;
                }

                if (type == typeof(DateTime) && DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateValue))
                {
                    value = dateValue;
                    return true;
                }

                value = Convert.ChangeType(text, type, CultureInfo.CurrentCulture);
                return true;
            }
            catch
            {
                value = DBNull.Value;
                return false;
            }
        }

        private void ShowInvalidCellMessage(string detail)
        {
            ShowCellMessage(detail, "Érvénytelen adat", mentionCleared: true);
        }

        private void ShowMissingValueMessage(string detail)
        {
            ShowCellMessage(detail, "Hiányzó adat", mentionCleared: false);
        }

        private void ShowCellMessage(string detail, string caption, bool mentionCleared)
        {
            if (_invalidValueHandled)
                return;

            _invalidValueHandled = true;
            string text = mentionCleared
                ? detail + Environment.NewLine + "A cella kiürítésre került."
                : detail;
            MessageBox.Show(this, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowInvalidCellMessage(string columnName, Type valueType)
        {
            ShowInvalidCellMessage($"A(z) {columnName} mező csak {FriendlyTypeName(valueType)} fogad el.");
        }

        private void ClearCell(int rowIndex, int columnIndex)
        {
            if (_grid.EditingControl != null)
                _grid.EditingControl.Text = string.Empty;
        }

        private void RejectInvalidCell(int rowIndex, int columnIndex, string message)
        {
            _cellValidationFailed = true;
            ShowInvalidCellMessage(message);
            ClearCell(rowIndex, columnIndex);
            BeginInvoke(() => BeginInvoke(() =>
            {
                _invalidValueHandled = false;
                FocusCellForEdit(rowIndex, columnIndex, clearValue: true);
            }));
        }

        private void RejectRequiredCell(int rowIndex, int columnIndex, string message)
        {
            _cellValidationFailed = true;
            ShowMissingValueMessage(message);
            BeginInvoke(() => BeginInvoke(() =>
            {
                _invalidValueHandled = false;
                FocusCellForEdit(rowIndex, columnIndex);
            }));
        }

        private void FocusCellForEdit(int rowIndex, int columnIndex, bool clearValue = false)
        {
            if (IsDisposed || _mode == Mode.Browse)
                return;
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return;
            if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
                return;

            Activate();
            _grid.Select();
            _grid.Focus();

            if (_grid.IsCurrentCellInEditMode)
            {
                _allowRowLeave = true;
                _suppressCellValidating = true;
                try
                {
                    _grid.EndEdit();
                }
                finally
                {
                    _allowRowLeave = false;
                    _suppressCellValidating = false;
                }
            }

            _grid.CurrentCell = _grid.Rows[rowIndex].Cells[columnIndex];
            if (clearValue)
                _grid.Rows[rowIndex].Cells[columnIndex].Value = DBNull.Value;
            _grid.BeginEdit(true);

            if (_grid.EditingControl is { } editor)
            {
                editor.Focus();
                if (editor is TextBox textBox)
                    textBox.SelectAll();
            }
        }

        private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_mode == Mode.Browse || _suppressCellValidating)
                return;

            string? text = e.FormattedValue?.ToString();

            if (IsTermekKodColumn(e.ColumnIndex))
            {
                if (!string.IsNullOrWhiteSpace(text))
                    return;

                RejectRequiredCell(e.RowIndex, e.ColumnIndex, RequiredFieldMessage(e.ColumnIndex));
                e.Cancel = true;
                return;
            }

            if (IsElabeColumn(e.ColumnIndex))
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    RejectRequiredCell(e.RowIndex, e.ColumnIndex, RequiredFieldMessage(e.ColumnIndex));
                    e.Cancel = true;
                    return;
                }

                DataColumn? elabeColumn = GetTableColumn(e.ColumnIndex);
                Type elabeType = elabeColumn?.DataType ?? typeof(double);
                if (TryConvertValue(text, elabeType, out _))
                    return;

                RejectInvalidCell(e.RowIndex, e.ColumnIndex, NumberFieldMessage(e.ColumnIndex));
                e.Cancel = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
                return;

            DataColumn? column = GetTableColumn(e.ColumnIndex);
            if (column == null || column.DataType == typeof(string))
                return;

            if (IsBizhoColumn(e.ColumnIndex))
            {
                if (TryParseBizho(text, out _))
                    return;

                RejectInvalidCell(e.RowIndex, e.ColumnIndex, BizhoInvalidMessage);
                e.Cancel = true;
                return;
            }

            if (TryConvertValue(text, column.DataType, out _))
                return;

            RejectInvalidCell(
                e.RowIndex,
                e.ColumnIndex,
                $"A(z) {_grid.Columns[e.ColumnIndex].HeaderText} mező csak {FriendlyTypeName(column.DataType)} fogad el.");
            e.Cancel = true;
        }

        private void Grid_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            e.Cancel = true;

            DataColumn? column = GetTableColumn(e.ColumnIndex);
            string header = e.ColumnIndex >= 0 && e.ColumnIndex < _grid.Columns.Count
                ? _grid.Columns[e.ColumnIndex].HeaderText
                : "mező";

            string message = IsBizhoColumn(e.ColumnIndex)
                ? BizhoInvalidMessage
                : IsElabeColumn(e.ColumnIndex)
                    ? NumberFieldMessage(e.ColumnIndex)
                    : $"A(z) {header} mező csak {FriendlyTypeName(column?.DataType ?? typeof(string))} fogad el.";

            RejectInvalidCell(e.RowIndex, e.ColumnIndex, message);
        }

        private void Grid_CellParsing(object? sender, DataGridViewCellParsingEventArgs e)
        {
            if (IsBizhoColumn(e.ColumnIndex))
            {
                string bizhoText = BizhoToUnpaddedText(e.Value);
                if (string.IsNullOrWhiteSpace(bizhoText))
                {
                    e.Value = DBNull.Value;
                    e.ParsingApplied = true;
                    _invalidValueHandled = false;
                    return;
                }

                if (TryParseBizho(bizhoText, out int bizho))
                {
                    e.Value = bizho;
                    e.ParsingApplied = true;
                    return;
                }

                // Do not let the default int converter accept 2222 (shown as 002222).
                e.Value = DBNull.Value;
                e.ParsingApplied = true;
                _cellValidationFailed = true;
                ShowInvalidCellMessage(BizhoInvalidMessage);
                return;
            }

            if (IsElabeColumn(e.ColumnIndex))
            {
                string elabeText = e.Value as string
                    ?? Convert.ToString(e.Value, CultureInfo.CurrentCulture)
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(elabeText))
                {
                    e.Value = DBNull.Value;
                    e.ParsingApplied = true;
                    return;
                }

                DataColumn? elabeColumn = GetTableColumn(e.ColumnIndex);
                Type elabeType = elabeColumn?.DataType ?? typeof(double);
                if (TryConvertValue(elabeText, elabeType, out object elabeValue))
                {
                    e.Value = elabeValue;
                    e.ParsingApplied = true;
                    return;
                }

                e.Value = DBNull.Value;
                e.ParsingApplied = true;
                _cellValidationFailed = true;
                ShowInvalidCellMessage(NumberFieldMessage(e.ColumnIndex));
                return;
            }

            if (e.Value is not string text)
                return;

            if (string.IsNullOrWhiteSpace(text))
            {
                e.Value = DBNull.Value;
                e.ParsingApplied = true;
                _invalidValueHandled = false;
                return;
            }

            DataColumn? column = GetTableColumn(e.ColumnIndex);
            if (column == null || column.DataType == typeof(string))
                return;

            if (TryConvertValue(text, column.DataType, out object converted))
            {
                e.Value = converted;
                e.ParsingApplied = true;
                return;
            }

            e.Value = DBNull.Value;
            e.ParsingApplied = true;
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_mode == Mode.Browse || GetDataRow(e.RowIndex) != _lockedDataRow)
                return;

            e.CellStyle.BackColor = EditRowBackColor;
            e.CellStyle.SelectionBackColor = EditRowSelectionBackColor;
            e.CellStyle.ForeColor = Color.Black;
            e.CellStyle.SelectionForeColor = Color.Black;
        }

        private void ApplyEditRowHighlight()
        {
            int rowIndex = FindRowIndex(_lockedDataRow);
            if (rowIndex >= 0)
                _grid.InvalidateRow(rowIndex);
            else
                _grid.Invalidate();
        }

        private void SetToolbarEditing(bool editing)
        {
            _btnNew.Visible = !editing;
            _btnEdit.Visible = !editing;
            _btnDelete.Visible = !editing;
            _btnSave.Visible = editing;
            _btnCancel.Visible = editing;
        }

        private void SetBusy(bool busy)
        {
            _btnNew.Enabled = !busy;
            _btnEdit.Enabled = !busy;
            _btnDelete.Enabled = !busy;
        }

        private bool TryGetCurrentRow(out DataRow row)
        {
            row = null!;
            if (_binding.Current is DataRowView view)
            {
                row = view.Row;
                return row.RowState != DataRowState.Detached;
            }
            return false;
        }

        private int FirstEditableColumnIndex()
        {
            if (_grid.Columns["BIZHO"] is { Visible: true } bizho)
                return bizho.Index;

            foreach (DataGridViewColumn column in _grid.Columns)
            {
                if (column.Visible && column.Name != "ID")
                    return column.Index;
            }

            return 0;
        }
    }
}
