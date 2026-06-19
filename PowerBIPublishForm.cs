namespace RefreshVIR
{
    public partial class PowerBIPublishForm : Form
    {
        private List<PowerBiWorkspace> workspaces = new();
        private PowerBiSession? _powerBiSession;
        private PowerBiWorkspaceSnapshot? _currentSnapshot;
        private bool suppressWorkspaceEvents;
        private int waitCursorDepth;

        public PowerBIPublishForm()
        {
            InitializeComponent();
            ApplicationBrand.Apply(this);
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

            _powerBiSession = await PowerBiApiClient.CreateSessionAsync();
            await LoadWorkspacesAsync();
        }

        private async void WorkspaceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (suppressWorkspaceEvents)
                return;

            if (workspaceComboBox.SelectedItem is PowerBiWorkspace workspace)
            {
                await Task.WhenAll(
                    LoadWorkspaceAccessEmailAsync(workspace),
                    LoadReportsAsync(workspace));
            }
            else
            {
                workspaceAccessLabel.Text = "";
                _currentSnapshot = null;
                BindReports(Array.Empty<PowerBiReportInfo>());
            }
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
                    if (_powerBiSession == null)
                        throw new InvalidOperationException("Power BI munkamenet nem elérhető.");

                    workspaces = (await PowerBIService.GetWorkspacesAsync(_powerBiSession)).ToList();

                    suppressWorkspaceEvents = true;
                    workspaceComboBox.DataSource = null;
                    workspaceComboBox.DisplayMember = nameof(PowerBiWorkspace.Name);
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
                        await Task.WhenAll(
                            LoadWorkspaceAccessEmailAsync(workspace),
                            LoadReportsAsync(workspace));
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

        private async Task LoadWorkspaceAccessEmailAsync(PowerBiWorkspace workspace)
        {
            try
            {
                if (_powerBiSession == null)
                    return;

                string accessEmail = await _powerBiSession.GetWorkspaceAccessEmailAsync(
                    workspace.Id,
                    workspace.Name);
                workspaceAccessLabel.Text = string.IsNullOrWhiteSpace(accessEmail)
                    ? ""
                    : $"Hozzáférés: {accessEmail}";
            }
            catch
            {
                workspaceAccessLabel.Text = "";
            }
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
                    if (_powerBiSession == null)
                        throw new InvalidOperationException("Power BI munkamenet nem elérhető.");

                    PowerBiWorkspaceSnapshot snapshot =
                        await PowerBIService.LoadWorkspaceSnapshotAsync(_powerBiSession, workspace.Id);

                    _currentSnapshot = snapshot;
                    BindReports(snapshot.Reports);

                    if (updateStatus)
                        statusLabel.Text = BuildReportsStatusText(snapshot.Reports.Count, snapshot.LoadWarnings);
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

        private void BindReports(IReadOnlyList<PowerBiReportInfo> reports) =>
            PowerBiReportsGridBinder.Bind(reportsGrid, reports);

        private void ReportsGrid_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e) =>
            PowerBiReportsGridBinder.ApplyRowStyles(reportsGrid);

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            PowerBiActionLogger.Log("Tallózás gomb megnyomva");

            using OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Power BI riport (*.pbix)|*.pbix",
                Title = "PBIX fájl kiválasztása"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                filePathTextBox.Text = dialog.FileName;
                PowerBiActionLogger.Log(
                    "PBIX fájl kiválasztva",
                    pbixPath: dialog.FileName);
            }
            else
            {
                PowerBiActionLogger.Log("PBIX tallózás megszakítva");
            }
        }

        private void CloseButton_Click(object? sender, EventArgs e)
        {
            PowerBiActionLogger.Log("Vissza gomb megnyomva");
            Close();
        }

        private async void PublishButton_Click(object? sender, EventArgs e)
        {
            if (!TryBuildPublishRequest(out PowerBiPublishRequest request, out string? validationMessage))
            {
                if (validationMessage != null)
                {
                    MessageBox.Show(
                        validationMessage,
                        "Figyelmeztetés",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return;
            }

            PowerBiActionLogger.Log(
                "Publikálás gomb megnyomva",
                request.Workspace,
                request.ReportName,
                request.PbixPath);

            if (!Authorization.ConfirmAllowedToPublishPowerBiReports(this))
            {
                PowerBiActionLogger.Log(
                    "Publikálás elutasítva",
                    request.Workspace,
                    request.ReportName,
                    request.PbixPath,
                    "ok=nincs jogosultság");
                return;
            }

            PowerBiExistingReportInfo? existingReport =
                PowerBiPublishWorkflow.TryGetExistingReportFromSnapshot(request)
                ?? await RunWithWaitCursorAsync(() =>
                    PowerBiPublishWorkflow.GetExistingReportAsync(request));

            if (existingReport == null)
            {
                await HandleMissingReportAsync(request);
                return;
            }

            if (MessageBox.Show(
                    existingReport.BuildConfirmationMessage(request.Workspace.Name),
                    "Power BI riport frissítése",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                PowerBiActionLogger.Log(
                    "Publikálás megszakítva",
                    request.Workspace,
                    request.ReportName,
                    request.PbixPath,
                    "ok=felhasználó elutasította");
                return;
            }

            await ExecutePublishFlowAsync(request);
        }

        private bool TryBuildPublishRequest(
            out PowerBiPublishRequest request,
            out string? validationMessage)
        {
            request = null!;
            validationMessage = null;

            PowerBiWorkspace? selectedWorkspace =
                workspaceComboBox.SelectedItem as PowerBiWorkspace;
            string? pbixPath = string.IsNullOrWhiteSpace(filePathTextBox.Text)
                ? null
                : filePathTextBox.Text;

            if (string.IsNullOrWhiteSpace(pbixPath))
            {
                PowerBiActionLogger.Log(
                    "Publikálás elutasítva",
                    selectedWorkspace,
                    detail: "ok=PBIX fájl nincs kiválasztva");
                validationMessage = "Válassz ki egy PBIX fájlt.";
                return false;
            }

            if (selectedWorkspace is not PowerBiWorkspace workspace)
            {
                PowerBiActionLogger.Log(
                    "Publikálás elutasítva",
                    reportName: Path.GetFileNameWithoutExtension(pbixPath),
                    pbixPath: pbixPath,
                    detail: "ok=munkaterület nincs kiválasztva");
                validationMessage = "Válassz ki egy munkaterületet.";
                return false;
            }

            request = new PowerBiPublishRequest
            {
                Session = RequirePowerBiSession(),
                Workspace = workspace,
                PbixPath = pbixPath,
                ReportName = Path.GetFileNameWithoutExtension(pbixPath),
                Snapshot = _currentSnapshot
            };

            return true;
        }

        private Task HandleMissingReportAsync(PowerBiPublishRequest request)
        {
            PowerBiActionLogger.Log(
                "Publikálás elutasítva",
                request.Workspace,
                request.ReportName,
                request.PbixPath,
                "ok=riport nem található a munkaterületen");

            string summary =
                $"A(z) '{request.ReportName}' nevű riport nem található a '{request.Workspace.Name}' munkaterületen.\n\n" +
                "Az alkalmazás csak meglévő riportok frissítését támogatja.";

            ErrorDialog.ShowError(this, "Riport nem található", summary, report =>
            {
                report.Add("Report name", request.ReportName);
                report.Add("Workspace", request.Workspace.Name);
                report.Add("Workspace ID", request.Workspace.Id.ToString());
                report.Add("PBIX file", request.PbixPath);
            });

            return Task.CompletedTask;
        }

        private async Task ExecutePublishFlowAsync(PowerBiPublishRequest request)
        {
            PowerBiActionLogger.Log(
                "Power BI riport frissítés indítva",
                request.Workspace,
                request.ReportName,
                request.PbixPath);

            request.Progress = CreateStatusProgress();
            SetPublishControlsEnabled(false);

            try
            {
                await RunWithWaitCursorAsync(async () =>
                    await PowerBiPublishWorkflow.PublishReportAsync(request));

                PowerBiActionLogger.Log(
                    "Power BI riport frissítés sikeres",
                    request.Workspace,
                    request.ReportName,
                    request.PbixPath);

                await RefreshReportGridAfterPublishAsync(request.Workspace);
                SetStatusText("Publikálás kész.");

                if (MessageBox.Show(
                        $"A(z) '{request.ReportName}' riport sikeresen frissítve a '{request.Workspace.Name}' munkaterületen.\n\n" +
                        "Szeretnéd frissíteni a munkaterület appját is?",
                        "Riport frissítve",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    await ExecuteAppUpdateAsync(request);
                }
                else
                {
                    PowerBiActionLogger.Log(
                        "Power BI app frissítés elutasítva",
                        request.Workspace,
                        request.ReportName,
                        request.PbixPath,
                        "ok=felhasználó elutasította");
                }

                SetStatusText("Frissítés kész.");
            }
            catch (Exception ex)
            {
                PowerBiActionLogger.Log(
                    "Power BI riport frissítés sikertelen",
                    request.Workspace,
                    request.ReportName,
                    request.PbixPath,
                    $"hiba={ex.Message}");

                ShowErrorMessage(
                    ex.Message,
                    "Power BI riport frissítés sikertelen",
                    ex,
                    report =>
                    {
                        report.Add("Operation", "Power BI report publish");
                        report.Add("Report name", request.ReportName);
                        report.Add("Workspace", request.Workspace.Name);
                        report.Add("Workspace ID", request.Workspace.Id.ToString());
                        report.Add("PBIX file", request.PbixPath);
                        report.Add("Status line", statusLabel.Text);
                    });
            }
            finally
            {
                ClearWaitCursor();
                SetPublishControlsEnabled(true);
            }
        }

        private async Task ExecuteAppUpdateAsync(PowerBiPublishRequest request)
        {
            PowerBiActionLogger.Log(
                "Power BI app frissítés indítva",
                request.Workspace,
                request.ReportName,
                request.PbixPath);

            request.AppUpdateProgress = CreateAppUpdateProgress();
            ShowAppUpdateProgress(0);

            try
            {
                await RunWithWaitCursorAsync(async () =>
                    await PowerBiPublishWorkflow.UpdateWorkspaceAppAsync(request));

                PowerBiActionLogger.Log(
                    "Power BI app frissítés sikeres",
                    request.Workspace,
                    request.ReportName,
                    request.PbixPath);
                SetAppUpdateProgress(100, "App frissítés kész.");

                MessageBox.Show(
                    $"A(z) '{request.Workspace.Name}' munkaterület appja sikeresen frissítve.",
                    "App frissítve",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception appEx)
            {
                PowerBiActionLogger.Log(
                    "Power BI app frissítés sikertelen",
                    request.Workspace,
                    request.ReportName,
                    request.PbixPath,
                    $"hiba={appEx.Message}");

                ShowErrorMessage(
                    appEx.Message,
                    "App frissítés sikertelen",
                    appEx,
                    report =>
                    {
                        report.Add("Operation", "Power BI app update");
                        report.Add("Workspace", request.Workspace.Name);
                        report.Add("Workspace ID", request.Workspace.Id.ToString());
                        report.Add("Report name", request.ReportName);
                        report.Add("PBIX file", request.PbixPath);
                        report.Add("Status line", statusLabel.Text);
                    });

                SetStatusText("App telepítés sikertelen.");
            }
            finally
            {
                HideAppUpdateProgress();
            }
        }

        private void SetPublishControlsEnabled(bool enabled)
        {
            publishButton.Enabled = enabled && workspaces.Count > 0;
            browseButton.Enabled = enabled;
            workspaceComboBox.Enabled = enabled && workspaces.Count > 0;
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

        private IProgress<AppUpdateProgressReport> CreateAppUpdateProgress() =>
            new Progress<AppUpdateProgressReport>(report => SetAppUpdateProgress(report.Percent, report.Message));

        private void ShowAppUpdateProgress(int percent)
        {
            if (InvokeRequired)
            {
                BeginInvoke(ShowAppUpdateProgress, percent);
                return;
            }

            appUpdateProgressBar.Visible = true;
            appUpdateProgressBar.Value = Math.Clamp(percent, 0, 100);
        }

        private void SetAppUpdateProgress(int percent, string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(SetAppUpdateProgress, percent, message);
                return;
            }

            appUpdateProgressBar.Visible = true;
            appUpdateProgressBar.Value = Math.Clamp(percent, 0, 100);
            statusLabel.Text = message;
        }

        private void HideAppUpdateProgress()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(HideAppUpdateProgress);
                return;
            }

            appUpdateProgressBar.Visible = false;
            appUpdateProgressBar.Value = 0;
        }

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

        private async Task RefreshReportGridAfterPublishAsync(PowerBiWorkspace workspace)
        {
            if (_currentSnapshot != null && _currentSnapshot.WorkspaceId == workspace.Id)
            {
                _currentSnapshot = await PowerBiPublishWorkflow.RefreshAfterPublishAsync(
                    RequirePowerBiSession(),
                    _currentSnapshot);
                BindReports(_currentSnapshot.Reports);
                return;
            }

            await LoadReportsAsync(workspace, updateStatus: false);
        }

        private PowerBiSession RequirePowerBiSession() =>
            _powerBiSession
            ?? throw new InvalidOperationException("Power BI munkamenet nem elérhető.");

        private static string BuildReportsStatusText(int reportCount, IReadOnlyList<string> loadWarnings)
        {
            string statusText = reportCount > 0
                ? $"{reportCount} riport betöltve."
                : "A kiválasztott munkaterületen nincs riport.";

            if (loadWarnings.Count == 0)
                return statusText;

            return $"{statusText} {string.Join(" ", loadWarnings)}";
        }

        private void PowerBIPublishForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _powerBiSession?.Dispose();
            _powerBiSession = null;
            _currentSnapshot = null;
            PowerBiActionLogger.Log("Power BI publikálás ablak bezárva");
        }
    }
}
