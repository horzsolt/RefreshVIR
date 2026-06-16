namespace RefreshVIR
{
    public class PowerBIPublishForm : Form
    {
        private TextBox filePathTextBox;
        private Button browseButton;
        private ComboBox workspaceComboBox;
        private Button publishButton;
        private Label statusLabel;
        private Button closeButton;
        private DataGridView reportsGrid;
        private List<PowerBiWorkspace> workspaces = new();
        private bool suppressWorkspaceEvents;
        private int waitCursorDepth;

        public PowerBIPublishForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Power BI riport publikálása";
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    this.Close();
            };
            this.FormClosed += PowerBIPublishForm_FormClosed;
            this.Load += PowerBIPublishForm_Load;

            TableLayoutPanel topLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(10)
            };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

            Label fileLabel = new Label
            {
                Text = "PBIX fájl:",
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };

            filePathTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true
            };

            browseButton = new Button
            {
                Text = "Tallózás...",
                Dock = DockStyle.Fill,
                Height = 28
            };
            browseButton.Click += BrowseButton_Click;

            Label workspaceLabel = new Label
            {
                Text = "Munkaterület:",
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };

            workspaceComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            workspaceComboBox.SelectedIndexChanged += WorkspaceComboBox_SelectedIndexChanged;

            publishButton = new Button
            {
                Text = "Publikálás",
                Width = 160,
                Height = 32,
                Anchor = AnchorStyles.Left
            };
            publishButton.Click += PublishButton_Click;

            statusLabel = new Label
            {
                Text = "",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                MaximumSize = new Size(900, 0)
            };

            Panel actionPanel = new Panel
            {
                Dock = DockStyle.Fill
            };
            actionPanel.Controls.Add(publishButton);
            actionPanel.Controls.Add(statusLabel);
            publishButton.Location = new Point(0, 0);
            statusLabel.Location = new Point(170, 8);

            topLayout.Controls.Add(fileLabel, 0, 0);
            topLayout.Controls.Add(filePathTextBox, 1, 0);
            topLayout.Controls.Add(browseButton, 2, 0);
            topLayout.Controls.Add(workspaceLabel, 0, 1);
            topLayout.Controls.Add(workspaceComboBox, 1, 1);
            topLayout.SetColumnSpan(workspaceComboBox, 2);
            topLayout.Controls.Add(actionPanel, 1, 2);
            topLayout.SetColumnSpan(actionPanel, 2);

            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            reportsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false
            };
            reportsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            reportsGrid.ColumnHeadersDefaultCellStyle.Font =
                new Font(reportsGrid.Font, FontStyle.Bold);
            reportsGrid.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;
            reportsGrid.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
            reportsGrid.DefaultCellStyle.SelectionForeColor = Color.Black;
            reportsGrid.DataBindingComplete += ReportsGrid_DataBindingComplete;

            closeButton = new Button
            {
                Text = "<< Vissza",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            closeButton.Click += CloseButton_Click;

            Controls.Add(reportsGrid);
            Controls.Add(closeButton);
            Controls.Add(topLayout);
        }

        private async void PowerBIPublishForm_Load(object? sender, EventArgs e)
        {
            string? configError = Configuration.GetPowerBiConfigurationError();
            if (configError != null)
            {
                publishButton.Enabled = false;
                statusLabel.Text = configError;
                MessageBox.Show(
                    configError,
                    "Power BI beállítás hiányzik",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            await LoadWorkspacesAsync();
        }

        private async void WorkspaceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (suppressWorkspaceEvents)
                return;

            if (workspaceComboBox.SelectedItem is PowerBiWorkspace workspace)
                await LoadReportsAsync(workspace);
            else
                BindReports(Array.Empty<PowerBiReportInfo>());
        }

        private async Task LoadWorkspacesAsync()
        {
            await RunWithWaitCursorAsync(async () =>
            {
                publishButton.Enabled = false;
                browseButton.Enabled = false;
                workspaceComboBox.Enabled = false;
                statusLabel.Text = "Munkaterületek betöltése...";

                try
                {
                    workspaces = (await PowerBIService.GetWorkspacesAsync()).ToList();

                    suppressWorkspaceEvents = true;
                    workspaceComboBox.DataSource = null;
                    workspaceComboBox.DisplayMember = nameof(PowerBiWorkspace.DisplayText);
                    workspaceComboBox.ValueMember = nameof(PowerBiWorkspace.Id);
                    workspaceComboBox.DataSource = workspaces;
                    suppressWorkspaceEvents = false;

                    if (workspaces.Count == 0)
                    {
                        BindReports(Array.Empty<PowerBiReportInfo>());
                        statusLabel.Text = "Nincs elérhető Power BI munkaterület.";
                    }
                    else if (workspaceComboBox.SelectedItem is PowerBiWorkspace workspace)
                    {
                        await LoadReportsAsync(workspace);
                    }
                    else
                    {
                        BindReports(Array.Empty<PowerBiReportInfo>());
                        statusLabel.Text = "Válassz PBIX fájlt és munkaterületet.";
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "Munkaterületek betöltése sikertelen.";
                    ShowErrorMessage(ex.Message, "Power BI hiba", ex, report =>
                    {
                        report.Add("Operation", "Load Power BI workspaces");
                        report.Add("Status line", statusLabel.Text);
                    });
                }
                finally
                {
                    publishButton.Enabled = workspaces.Count > 0;
                    browseButton.Enabled = true;
                    workspaceComboBox.Enabled = workspaces.Count > 0;
                }
            });
        }

        private async Task LoadReportsAsync(PowerBiWorkspace workspace, bool updateStatus = true)
        {
            await RunWithWaitCursorAsync(async () =>
            {
                publishButton.Enabled = false;
                browseButton.Enabled = false;
                workspaceComboBox.Enabled = false;

                if (updateStatus)
                    statusLabel.Text = "Riportok betöltése...";

                try
                {
                    IReadOnlyList<PowerBiReportInfo> reports =
                        await PowerBIService.GetWorkspaceReportsAsync(workspace.Id);

                    BindReports(reports);

                    if (updateStatus)
                    {
                        statusLabel.Text = reports.Count > 0
                            ? $"{reports.Count} riport betöltve."
                            : "A kiválasztott munkaterületen nincs riport.";
                    }
                }
                catch (Exception ex)
                {
                    BindReports(Array.Empty<PowerBiReportInfo>());
                    statusLabel.Text = "Riportok betöltése sikertelen.";
                    ShowErrorMessage(ex.Message, "Power BI hiba", ex, report =>
                    {
                        report.Add("Operation", "Load Power BI reports");
                        report.Add("Workspace", workspace.Name);
                        report.Add("Workspace ID", workspace.Id.ToString());
                        report.Add("Status line", statusLabel.Text);
                    });
                }
                finally
                {
                    publishButton.Enabled = workspaces.Count > 0;
                    browseButton.Enabled = true;
                    workspaceComboBox.Enabled = workspaces.Count > 0;
                }
            });
        }

        private void BindReports(IReadOnlyList<PowerBiReportInfo> reports)
        {
            reportsGrid.DataSource = null;
            reportsGrid.Columns.Clear();
            reportsGrid.AutoGenerateColumns = false;

            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.ReportName),
                HeaderText = "Riport neve",
                FillWeight = 180
            });
            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.DataSourceDisplay),
                HeaderText = "Adatforrás",
                FillWeight = 140
            });
            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.ReportType),
                HeaderText = "Típus",
                FillWeight = 110
            });
            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.LastUploadDisplay),
                HeaderText = "Utolsó feltöltés",
                FillWeight = 120
            });
            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.LastRefreshDisplay),
                HeaderText = "Utolsó adatfrissítés",
                FillWeight = 120
            });
            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.NextRefreshDisplay),
                HeaderText = "Következő adatfrissítés",
                FillWeight = 120
            });
            reportsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(PowerBiReportInfo.ScheduleDisplay),
                HeaderText = "Ütemezés",
                FillWeight = 90
            });

            reportsGrid.DataSource = reports.ToList();
        }

        private void ReportsGrid_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in reportsGrid.Rows)
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
                    row.DefaultCellStyle.BackColor = reportsGrid.DefaultCellStyle.BackColor;
                    row.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
                    row.DefaultCellStyle.SelectionForeColor = Color.Black;
                }
            }
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            LogPowerBiAction("Tallózás gomb megnyomva");

            using OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Power BI riport (*.pbix)|*.pbix",
                Title = "PBIX fájl kiválasztása"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                filePathTextBox.Text = dialog.FileName;
                LogPowerBiAction(
                    "PBIX fájl kiválasztva",
                    pbixPath: dialog.FileName);
            }
            else
            {
                LogPowerBiAction("PBIX tallózás megszakítva");
            }
        }

        private void CloseButton_Click(object? sender, EventArgs e)
        {
            LogPowerBiAction("Vissza gomb megnyomva");
            Close();
        }

        private void LogPowerBiAction(
            string action,
            PowerBiWorkspace? workspace = null,
            string? reportName = null,
            string? pbixPath = null,
            string? detail = null)
        {
            workspace ??= workspaceComboBox.SelectedItem as PowerBiWorkspace;
            pbixPath ??= string.IsNullOrWhiteSpace(filePathTextBox.Text)
                ? null
                : filePathTextBox.Text;
            reportName ??= pbixPath == null
                ? null
                : Path.GetFileNameWithoutExtension(pbixPath);

            List<string> parts = new() { action };

            if (!string.IsNullOrWhiteSpace(reportName))
                parts.Add($"riport={reportName}");

            if (!string.IsNullOrWhiteSpace(pbixPath))
                parts.Add($"pbix={pbixPath}");

            if (workspace != null)
                parts.Add($"munkaterület={workspace.Name}");

            if (!string.IsNullOrWhiteSpace(detail))
                parts.Add(detail);

            SQLUtils.LogAction(string.Join(" | ", parts));
        }

        private async void PublishButton_Click(object? sender, EventArgs e)
        {
            PowerBiWorkspace? selectedWorkspace =
                workspaceComboBox.SelectedItem as PowerBiWorkspace;
            string? pbixPath = string.IsNullOrWhiteSpace(filePathTextBox.Text)
                ? null
                : filePathTextBox.Text;
            string? reportName = pbixPath == null
                ? null
                : Path.GetFileNameWithoutExtension(pbixPath);

            LogPowerBiAction(
                "Publikálás gomb megnyomva",
                selectedWorkspace,
                reportName,
                pbixPath);

            if (!Authorization.ConfirmAllowedToPublishPowerBiReports(this))
            {
                LogPowerBiAction(
                    "Publikálás elutasítva",
                    selectedWorkspace,
                    reportName,
                    pbixPath,
                    "ok=nincs jogosultság");
                return;
            }

            if (string.IsNullOrWhiteSpace(filePathTextBox.Text))
            {
                LogPowerBiAction(
                    "Publikálás elutasítva",
                    selectedWorkspace,
                    detail: "ok=PBIX fájl nincs kiválasztva");
                MessageBox.Show(
                    "Válassz ki egy PBIX fájlt.",
                    "Figyelmeztetés",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (selectedWorkspace is not PowerBiWorkspace workspace)
            {
                LogPowerBiAction(
                    "Publikálás elutasítva",
                    reportName: reportName,
                    pbixPath: pbixPath,
                    detail: "ok=munkaterület nincs kiválasztva");
                MessageBox.Show(
                    "Válassz ki egy munkaterületet.",
                    "Figyelmeztetés",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            reportName = Path.GetFileNameWithoutExtension(filePathTextBox.Text);

            PowerBiExistingReportInfo? existingReport = await RunWithWaitCursorAsync(() =>
                PowerBIService.GetExistingReportAsync(workspace.Id, reportName));

            if (existingReport == null)
            {
                LogPowerBiAction(
                    "Publikálás elutasítva",
                    workspace,
                    reportName,
                    filePathTextBox.Text,
                    "ok=riport nem található a munkaterületen");

                string summary =
                    $"A(z) '{reportName}' nevű riport nem található a '{workspace.Name}' munkaterületen.\n\n" +
                    "Az alkalmazás csak meglévő riportok frissítését támogatja.";

                ErrorDialog.ShowError(this, "Riport nem található", summary, report =>
                {
                    report.Add("Report name", reportName);
                    report.Add("Workspace", workspace.Name);
                    report.Add("Workspace ID", workspace.Id.ToString());
                    report.Add("PBIX file", filePathTextBox.Text);
                });
                return;
            }

            var confirm = MessageBox.Show(
                existingReport.BuildConfirmationMessage(workspace.Name),
                "Power BI riport frissítése",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                LogPowerBiAction(
                    "Publikálás megszakítva",
                    workspace,
                    reportName,
                    filePathTextBox.Text,
                    "ok=felhasználó elutasította");
                return;
            }

            LogPowerBiAction(
                "Power BI riport frissítés indítva",
                workspace,
                reportName,
                filePathTextBox.Text);

            IProgress<string> progress = CreateStatusProgress();

            publishButton.Enabled = false;
            browseButton.Enabled = false;
            workspaceComboBox.Enabled = false;

            try
            {
                await RunWithWaitCursorAsync(async () =>
                {
                    await PowerBIService.PublishPbixAsync(
                        workspace.Id,
                        filePathTextBox.Text,
                        progress);
                });

                LogPowerBiAction(
                    "Power BI riport frissítés sikeres",
                    workspace,
                    reportName,
                    filePathTextBox.Text);

                await LoadReportsAsync(workspace, updateStatus: false);
                SetStatusText("Publikálás kész.");

                DialogResult updateApp = MessageBox.Show(
                    $"A(z) '{reportName}' riport sikeresen frissítve a '{workspace.Name}' munkaterületen.\n\n" +
                    "Szeretnéd frissíteni a munkaterület appját is?",
                    "Riport frissítve",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (updateApp == DialogResult.Yes)
                {
                    LogPowerBiAction(
                        "Power BI app frissítés indítva",
                        workspace,
                        reportName,
                        filePathTextBox.Text);

                    SetStatusText("App frissítése...");

                    try
                    {
                        await RunWithWaitCursorAsync(async () =>
                        {
                            await PowerBIService.UpdateWorkspaceAppAsync(workspace.Id, progress);
                        });

                        LogPowerBiAction(
                            "Power BI app frissítés sikeres",
                            workspace,
                            reportName,
                            filePathTextBox.Text);
                        SetStatusText("App frissítés kész.");

                        MessageBox.Show(
                            $"A(z) '{workspace.Name}' munkaterület appja sikeresen frissítve.",
                            "App frissítve",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception appEx)
                    {
                        LogPowerBiAction(
                            "Power BI app frissítés sikertelen",
                            workspace,
                            reportName,
                            filePathTextBox.Text,
                            $"hiba={appEx.Message}");

                        ShowErrorMessage(
                            appEx.Message,
                            "App frissítés sikertelen",
                            appEx,
                            report =>
                            {
                                report.Add("Operation", "Power BI app update");
                                report.Add("Workspace", workspace.Name);
                                report.Add("Workspace ID", workspace.Id.ToString());
                                report.Add("Report name", reportName);
                                report.Add("PBIX file", filePathTextBox.Text);
                                report.Add("Status line", statusLabel.Text);
                            });

                        SetStatusText("App telepítés sikertelen.");
                        return;
                    }
                }
                else
                {
                    LogPowerBiAction(
                        "Power BI app frissítés elutasítva",
                        workspace,
                        reportName,
                        filePathTextBox.Text,
                        "ok=felhasználó elutasította");
                }

                SetStatusText("Frissítés kész.");
            }
            catch (Exception ex)
            {
                LogPowerBiAction(
                    "Power BI riport frissítés sikertelen",
                    workspace,
                    reportName,
                    filePathTextBox.Text,
                    $"hiba={ex.Message}");

                ShowErrorMessage(
                    ex.Message,
                    "Power BI riport frissítés sikertelen",
                    ex,
                    report =>
                    {
                        report.Add("Operation", "Power BI report publish");
                        report.Add("Report name", reportName);
                        report.Add("Workspace", workspace.Name);
                        report.Add("Workspace ID", workspace.Id.ToString());
                        report.Add("PBIX file", filePathTextBox.Text);
                        report.Add("Status line", statusLabel.Text);
                    });
            }
            finally
            {
                ClearWaitCursor();
                publishButton.Enabled = true;
                browseButton.Enabled = true;
                workspaceComboBox.Enabled = true;
            }
        }

        private void BeginWaitCursor()
        {
            if (waitCursorDepth++ == 0)
            {
                UseWaitCursor = true;
                Cursor = Cursors.WaitCursor;
            }
        }

        private void EndWaitCursor()
        {
            if (waitCursorDepth <= 0)
                return;

            if (--waitCursorDepth == 0)
                ResetWaitCursor();
        }

        private void ResetWaitCursor()
        {
            UseWaitCursor = false;
            Cursor = Cursors.Default;
            reportsGrid.Cursor = Cursors.Default;
            Cursor.Current = Cursors.Default;
        }

        private async Task RunWithWaitCursorAsync(Func<Task> action)
        {
            BeginWaitCursor();
            try
            {
                await action();
            }
            finally
            {
                EndWaitCursor();
            }
        }

        private async Task<T> RunWithWaitCursorAsync<T>(Func<Task<T>> action)
        {
            BeginWaitCursor();
            try
            {
                return await action();
            }
            finally
            {
                EndWaitCursor();
            }
        }

        private IProgress<string> CreateStatusProgress() =>
            new Progress<string>(SetStatusText);

        private void SetStatusText(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
                BeginInvoke(SetStatusText, message);
            else
                statusLabel.Text = message;
        }

        private void ShowErrorMessage(
            string summary,
            string title,
            Exception? exception = null,
            Action<ErrorReport.ErrorReportBuilder>? configure = null)
        {
            ClearWaitCursor();

            summary = ErrorReport.NormalizeDisplaySummary(summary);

            if (exception != null)
                ErrorDialog.ShowError(this, title, summary, exception, configure);
            else if (configure != null)
                ErrorDialog.ShowError(this, title, summary, configure);
            else
                ErrorDialog.ShowError(this, title, summary);
        }

        private void ClearWaitCursor()
        {
            while (waitCursorDepth > 0)
                EndWaitCursor();

            ResetWaitCursor();
        }

        private void PowerBIPublishForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            LogPowerBiAction("Power BI publikálás ablak bezárva");
        }
    }
}
