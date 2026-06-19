namespace RefreshVIR
{
    partial class PowerBIPublishForm
    {
        private TextBox filePathTextBox;
        private Button browseButton;
        private ComboBox workspaceComboBox;
        private Label workspaceAccessLabel;
        private Button publishButton;
        private Label statusLabel;
        private ProgressBar appUpdateProgressBar;
        private Button closeButton;
        private DataGridView reportsGrid;

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
                RowCount = 4,
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

            workspaceAccessLabel = new Label
            {
                Text = "",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                MaximumSize = new Size(900, 0)
            };

            statusLabel = new Label
            {
                Text = "",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Dock = DockStyle.Fill,
                MaximumSize = new Size(900, 0)
            };

            appUpdateProgressBar = new ProgressBar
            {
                Height = 18,
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };

            publishButton = new Button
            {
                Text = "Publikálás",
                Width = 160,
                Height = 32,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 0, 0)
            };
            publishButton.Click += PublishButton_Click;

            TableLayoutPanel actionArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            actionArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            actionArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionArea.Controls.Add(statusLabel, 0, 0);
            actionArea.Controls.Add(appUpdateProgressBar, 0, 1);
            actionArea.Controls.Add(publishButton, 0, 2);

            topLayout.Controls.Add(fileLabel, 0, 0);
            topLayout.Controls.Add(filePathTextBox, 1, 0);
            topLayout.Controls.Add(browseButton, 2, 0);
            topLayout.Controls.Add(workspaceLabel, 0, 1);
            topLayout.Controls.Add(workspaceComboBox, 1, 1);
            topLayout.SetColumnSpan(workspaceComboBox, 2);
            topLayout.Controls.Add(workspaceAccessLabel, 1, 2);
            topLayout.SetColumnSpan(workspaceAccessLabel, 2);
            topLayout.Controls.Add(actionArea, 1, 3);
            topLayout.SetColumnSpan(actionArea, 2);

            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
    }
}
