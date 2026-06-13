namespace RefreshVIR
{
    public class PowerBIPublishForm : Form
    {
        private TextBox filePathTextBox;
        private Button browseButton;
        private ComboBox workspaceComboBox;
        private RadioButton overwriteRadio;
        private RadioButton uniqueNameRadio;
        private Button publishButton;
        private Label statusLabel;
        private Button closeButton;
        private List<PowerBiWorkspace> workspaces = new();

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
            this.Text = "Power BI jelentés publikálása";
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    this.Close();
            };
            this.FormClosed += PowerBIPublishForm_FormClosed;
            this.Load += PowerBIPublishForm_Load;

            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 170,
                Padding = new Padding(10)
            };

            Label fileLabel = new Label
            {
                Text = "PBIX fájl:",
                AutoSize = true,
                Left = 10,
                Top = 15
            };

            filePathTextBox = new TextBox
            {
                Left = 100,
                Top = 12,
                Width = 700,
                ReadOnly = true
            };

            browseButton = new Button
            {
                Text = "Tallózás...",
                Left = 810,
                Top = 10,
                Width = 120,
                Height = 28
            };
            browseButton.Click += BrowseButton_Click;

            Label workspaceLabel = new Label
            {
                Text = "Munkaterület:",
                AutoSize = true,
                Left = 10,
                Top = 55
            };

            workspaceComboBox = new ComboBox
            {
                Left = 100,
                Top = 52,
                Width = 830,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            Label conflictLabel = new Label
            {
                Text = "Névütközés:",
                AutoSize = true,
                Left = 10,
                Top = 95
            };

            overwriteRadio = new RadioButton
            {
                Text = "Meglévő felülírása",
                Checked = true,
                Left = 100,
                Top = 92,
                AutoSize = true
            };

            uniqueNameRadio = new RadioButton
            {
                Text = "Egyedi név létrehozása",
                Left = 320,
                Top = 92,
                AutoSize = true
            };

            publishButton = new Button
            {
                Text = "Publikálás",
                Left = 100,
                Top = 125,
                Width = 160,
                Height = 32
            };
            publishButton.Click += PublishButton_Click;

            statusLabel = new Label
            {
                Text = "",
                AutoSize = true,
                Left = 280,
                Top = 132,
                MaximumSize = new Size(650, 0)
            };

            topPanel.Controls.Add(fileLabel);
            topPanel.Controls.Add(filePathTextBox);
            topPanel.Controls.Add(browseButton);
            topPanel.Controls.Add(workspaceLabel);
            topPanel.Controls.Add(workspaceComboBox);
            topPanel.Controls.Add(conflictLabel);
            topPanel.Controls.Add(overwriteRadio);
            topPanel.Controls.Add(uniqueNameRadio);
            topPanel.Controls.Add(publishButton);
            topPanel.Controls.Add(statusLabel);

            closeButton = new Button
            {
                Text = "<< Vissza",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            closeButton.Click += (s, e) => this.Close();

            Controls.Add(closeButton);
            Controls.Add(topPanel);
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

        private async Task LoadWorkspacesAsync()
        {
            try
            {
                UseWaitCursor = true;
                publishButton.Enabled = false;
                browseButton.Enabled = false;
                statusLabel.Text = "Munkaterületek betöltése...";

                workspaces = (await PowerBIService.GetWorkspacesAsync()).ToList();
                workspaceComboBox.DataSource = null;
                workspaceComboBox.DisplayMember = nameof(PowerBiWorkspace.Name);
                workspaceComboBox.ValueMember = nameof(PowerBiWorkspace.Id);
                workspaceComboBox.DataSource = workspaces;

                statusLabel.Text = workspaces.Count > 0
                    ? "Válassz PBIX fájlt és munkaterületet."
                    : "Nincs elérhető Power BI munkaterület.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Munkaterületek betöltése sikertelen.";
                MessageBox.Show(
                    ex.Message,
                    "Power BI hiba",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                publishButton.Enabled = workspaces.Count > 0;
                browseButton.Enabled = true;
            }
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Power BI jelentés (*.pbix)|*.pbix",
                Title = "PBIX fájl kiválasztása"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                filePathTextBox.Text = dialog.FileName;
        }

        private async void PublishButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(filePathTextBox.Text))
            {
                MessageBox.Show(
                    "Válassz ki egy PBIX fájlt.",
                    "Figyelmeztetés",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (workspaceComboBox.SelectedItem is not PowerBiWorkspace workspace)
            {
                MessageBox.Show(
                    "Válassz ki egy munkaterületet.",
                    "Figyelmeztetés",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string fileName = Path.GetFileName(filePathTextBox.Text);
            string nameConflict = overwriteRadio.Checked
                ? "CreateOrOverwrite"
                : "GenerateUniqueName";

            var confirm = MessageBox.Show(
                $"Biztosan publikálni szeretnéd?\n\nFájl: {fileName}\nMunkaterület: {workspace.Name}",
                "Power BI publikálás",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            SQLUtils.LogAction($"Power BI jelentés publikálása: {fileName} -> {workspace.Name}");

            publishButton.Enabled = false;
            browseButton.Enabled = false;
            workspaceComboBox.Enabled = false;

            var progress = new Progress<string>(message => statusLabel.Text = message);

            try
            {
                UseWaitCursor = true;

                await PowerBIService.PublishPbixAsync(
                    workspace.Id,
                    filePathTextBox.Text,
                    nameConflict,
                    progress);

                SQLUtils.LogAction($"Power BI publikálás sikeres: {fileName} -> {workspace.Name}");

                MessageBox.Show(
                    $"A(z) '{fileName}' jelentés sikeresen publikálva a '{workspace.Name}' munkaterületre.",
                    "Siker",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SQLUtils.LogAction($"Power BI publikálás sikertelen: {fileName} -> {workspace.Name}");

                MessageBox.Show(
                    ex.Message,
                    "Power BI publikálás sikertelen",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                publishButton.Enabled = true;
                browseButton.Enabled = true;
                workspaceComboBox.Enabled = true;
            }
        }

        private void PowerBIPublishForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SQLUtils.LogAction("Power BI publikálás ablak bezárva");
        }
    }
}
