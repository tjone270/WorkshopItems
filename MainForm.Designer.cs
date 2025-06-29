namespace WorkshopItems {
    partial class MainForm {
#pragma warning disable CS8669
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing) {
            if (disposing) {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _steam?.Dispose();

                if (_steamCmdProcess != null) {
                    _steamCmdProcess.Dispose();
                }

                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            labelNoWorkshopItems = new Label();
            listViewWorkshopItems = new ListView();
            colWorkshopId = new ColumnHeader();
            colWorkshopTitle = new ColumnHeader();
            colWorkshopSize = new ColumnHeader();
            colWorkshopPath = new ColumnHeader();
            colWorkshopLastUpdated = new ColumnHeader();
            colWorkshopUpdateStatus = new ColumnHeader();
            contextMenuStripWorkshopItems = new ContextMenuStrip(components);
            updateSelectedItemToolStripMenuItem = new ToolStripMenuItem();
            forceDownloadToolStripMenuItem = new ToolStripMenuItem();
            openWorkshopPageToolStripMenuItem = new ToolStripMenuItem();
            menuStrip = new MenuStrip();
            populateToolStripMenuItem = new ToolStripMenuItem();
            deleteWorkshopDataToolStripMenuItem = new ToolStripMenuItem();
            openInExplorerToolStripMenuItem = new ToolStripMenuItem();
            repairInconsistenciesToolStripMenuItem = new ToolStripMenuItem();
            updateWorkshopItemsToolStripMenuItem = new ToolStripMenuItem();
            resetGameWorkshopToolStripMenuItem = new ToolStripMenuItem();
            validateGameFilesToolStripMenuItem = new ToolStripMenuItem();
            panelConsole = new Panel();
            textBoxConsole = new TextBox();
            panelConsoleTitle = new Panel();
            buttonCloseConsole = new Button();
            labelConsoleTitle = new Label();
            splitterConsole = new Splitter();
            contextMenuStripWorkshopItems.SuspendLayout();
            menuStrip.SuspendLayout();
            panelConsole.SuspendLayout();
            panelConsoleTitle.SuspendLayout();
            SuspendLayout();
            // 
            // labelNoWorkshopItems
            // 
            labelNoWorkshopItems.BackColor = SystemColors.ControlDark;
            labelNoWorkshopItems.Dock = DockStyle.Fill;
            labelNoWorkshopItems.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            labelNoWorkshopItems.Location = new Point(0, 24);
            labelNoWorkshopItems.Name = "labelNoWorkshopItems";
            labelNoWorkshopItems.Size = new Size(1100, 329);
            labelNoWorkshopItems.TabIndex = 1;
            labelNoWorkshopItems.Text = "Click 'Populate'";
            labelNoWorkshopItems.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // listViewWorkshopItems
            // 
            listViewWorkshopItems.Columns.AddRange(new ColumnHeader[] { colWorkshopId, colWorkshopTitle, colWorkshopSize, colWorkshopPath, colWorkshopLastUpdated, colWorkshopUpdateStatus });
            listViewWorkshopItems.ContextMenuStrip = contextMenuStripWorkshopItems;
            listViewWorkshopItems.Dock = DockStyle.Fill;
            listViewWorkshopItems.Font = new Font("Segoe UI", 9F);
            listViewWorkshopItems.FullRowSelect = true;
            listViewWorkshopItems.Location = new Point(0, 24);
            listViewWorkshopItems.Name = "listViewWorkshopItems";
            listViewWorkshopItems.ShowGroups = false;
            listViewWorkshopItems.Size = new Size(1100, 329);
            listViewWorkshopItems.TabIndex = 0;
            listViewWorkshopItems.UseCompatibleStateImageBehavior = false;
            listViewWorkshopItems.View = View.Details;
            listViewWorkshopItems.Visible = false;
            listViewWorkshopItems.ColumnClick += ListViewWorkshopItems_ColumnClick;
            listViewWorkshopItems.SelectedIndexChanged += ListViewWorkshopItems_SelectedIndexChanged;
            // 
            // colWorkshopId
            // 
            colWorkshopId.Text = "ID";
            colWorkshopId.Width = 105;
            // 
            // colWorkshopTitle
            // 
            colWorkshopTitle.Text = "Title";
            colWorkshopTitle.Width = 300;
            // 
            // colWorkshopSize
            // 
            colWorkshopSize.Text = "Size";
            colWorkshopSize.Width = 80;
            // 
            // colWorkshopPath
            // 
            colWorkshopPath.Text = "Data Path";
            colWorkshopPath.Width = 320;
            // 
            // colWorkshopLastUpdated
            // 
            colWorkshopLastUpdated.Text = "Last Updated";
            colWorkshopLastUpdated.Width = 120;
            // 
            // colWorkshopUpdateStatus
            // 
            colWorkshopUpdateStatus.Text = "Update Status";
            colWorkshopUpdateStatus.Width = 125;
            // 
            // contextMenuStripWorkshopItems
            // 
            contextMenuStripWorkshopItems.Items.AddRange(new ToolStripItem[] { updateSelectedItemToolStripMenuItem, forceDownloadToolStripMenuItem, openWorkshopPageToolStripMenuItem });
            contextMenuStripWorkshopItems.Name = "contextMenuStripWorkshopItems";
            contextMenuStripWorkshopItems.Size = new Size(200, 70);
            // 
            // updateSelectedItemToolStripMenuItem
            // 
            updateSelectedItemToolStripMenuItem.Name = "updateSelectedItemToolStripMenuItem";
            updateSelectedItemToolStripMenuItem.Size = new Size(199, 22);
            updateSelectedItemToolStripMenuItem.Text = "&Update Selected Item(s)";
            updateSelectedItemToolStripMenuItem.Click += UpdateSelectedItemToolStripMenuItem_Click;
            // 
            // forceDownloadToolStripMenuItem
            // 
            forceDownloadToolStripMenuItem.Name = "forceDownloadToolStripMenuItem";
            forceDownloadToolStripMenuItem.Size = new Size(199, 22);
            forceDownloadToolStripMenuItem.Text = "&Force Download";
            forceDownloadToolStripMenuItem.Click += ForceDownloadToolStripMenuItem_Click;
            // 
            // openWorkshopPageToolStripMenuItem
            // 
            openWorkshopPageToolStripMenuItem.Name = "openWorkshopPageToolStripMenuItem";
            openWorkshopPageToolStripMenuItem.Size = new Size(199, 22);
            openWorkshopPageToolStripMenuItem.Text = "Open &Workshop Page";
            openWorkshopPageToolStripMenuItem.Click += OpenWorkshopPageToolStripMenuItem_Click;
            // 
            // menuStrip
            // 
            menuStrip.Items.AddRange(new ToolStripItem[] { populateToolStripMenuItem, deleteWorkshopDataToolStripMenuItem, openInExplorerToolStripMenuItem, repairInconsistenciesToolStripMenuItem, updateWorkshopItemsToolStripMenuItem, validateGameFilesToolStripMenuItem, resetGameWorkshopToolStripMenuItem });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Size = new Size(1100, 24);
            menuStrip.TabIndex = 3;
            menuStrip.Text = "menuStrip";
            // 
            // populateToolStripMenuItem
            // 
            populateToolStripMenuItem.Name = "populateToolStripMenuItem";
            populateToolStripMenuItem.Size = new Size(66, 20);
            populateToolStripMenuItem.Text = "&Populate";
            populateToolStripMenuItem.Click += PopulateToolStripMenuItem_Click;
            // 
            // deleteWorkshopDataToolStripMenuItem
            // 
            deleteWorkshopDataToolStripMenuItem.Name = "deleteWorkshopDataToolStripMenuItem";
            deleteWorkshopDataToolStripMenuItem.Size = new Size(136, 20);
            deleteWorkshopDataToolStripMenuItem.Text = "&Delete Workshop Data";
            deleteWorkshopDataToolStripMenuItem.Visible = false;
            deleteWorkshopDataToolStripMenuItem.Click += DeleteWorkshopDataToolStripMenuItem_Click;
            // 
            // openInExplorerToolStripMenuItem
            // 
            openInExplorerToolStripMenuItem.Name = "openInExplorerToolStripMenuItem";
            openInExplorerToolStripMenuItem.Size = new Size(106, 20);
            openInExplorerToolStripMenuItem.Text = "&Open in Explorer";
            openInExplorerToolStripMenuItem.Visible = false;
            openInExplorerToolStripMenuItem.Click += OpenInExplorerToolStripMenuItem_Click;
            // 
            // repairInconsistenciesToolStripMenuItem
            // 
            repairInconsistenciesToolStripMenuItem.Name = "repairInconsistenciesToolStripMenuItem";
            repairInconsistenciesToolStripMenuItem.Size = new Size(135, 20);
            repairInconsistenciesToolStripMenuItem.Text = "&Repair Inconsistencies";
            repairInconsistenciesToolStripMenuItem.Visible = false;
            repairInconsistenciesToolStripMenuItem.Click += RepairInconsistenciesToolStripMenuItem_Click;
            // 
            // updateWorkshopItemsToolStripMenuItem
            // 
            updateWorkshopItemsToolStripMenuItem.Name = "updateWorkshopItemsToolStripMenuItem";
            updateWorkshopItemsToolStripMenuItem.Size = new Size(146, 20);
            updateWorkshopItemsToolStripMenuItem.Text = "&Update Workshop Items";
            updateWorkshopItemsToolStripMenuItem.Visible = false;
            updateWorkshopItemsToolStripMenuItem.Click += UpdateWorkshopItemsToolStripMenuItem_Click;
            // 
            // resetGameWorkshopToolStripMenuItem
            // 
            resetGameWorkshopToolStripMenuItem.ForeColor = SystemColors.ControlText;
            resetGameWorkshopToolStripMenuItem.Name = "resetGameWorkshopToolStripMenuItem";
            resetGameWorkshopToolStripMenuItem.Size = new Size(104, 20);
            resetGameWorkshopToolStripMenuItem.Text = "Reset &Workshop";
            resetGameWorkshopToolStripMenuItem.Visible = false;
            resetGameWorkshopToolStripMenuItem.Click += ResetGameWorkshopToolStripMenuItem_Click;
            // 
            // validateGameFilesToolStripMenuItem
            // 
            validateGameFilesToolStripMenuItem.Name = "validateGameFilesToolStripMenuItem";
            validateGameFilesToolStripMenuItem.Size = new Size(86, 20);
            validateGameFilesToolStripMenuItem.Text = "&Validate Files";
            validateGameFilesToolStripMenuItem.Visible = false;
            validateGameFilesToolStripMenuItem.Click += ValidateGameFilesToolStripMenuItem_Click;
            // 
            // panelConsole
            // 
            panelConsole.BackColor = Color.Black;
            panelConsole.Controls.Add(textBoxConsole);
            panelConsole.Controls.Add(panelConsoleTitle);
            panelConsole.Dock = DockStyle.Bottom;
            panelConsole.Location = new Point(0, 356);
            panelConsole.Name = "panelConsole";
            panelConsole.Size = new Size(1100, 200);
            panelConsole.TabIndex = 5;
            panelConsole.Visible = false;
            // 
            // textBoxConsole
            // 
            textBoxConsole.BackColor = Color.Black;
            textBoxConsole.Dock = DockStyle.Fill;
            textBoxConsole.Font = new Font("Consolas", 9F);
            textBoxConsole.ForeColor = Color.LightGray;
            textBoxConsole.Location = new Point(0, 25);
            textBoxConsole.Multiline = true;
            textBoxConsole.Name = "textBoxConsole";
            textBoxConsole.ReadOnly = true;
            textBoxConsole.ScrollBars = ScrollBars.Both;
            textBoxConsole.Size = new Size(1100, 175);
            textBoxConsole.TabIndex = 1;
            textBoxConsole.WordWrap = false;
            // 
            // panelConsoleTitle
            // 
            panelConsoleTitle.BackColor = SystemColors.ControlDark;
            panelConsoleTitle.Controls.Add(buttonCloseConsole);
            panelConsoleTitle.Controls.Add(labelConsoleTitle);
            panelConsoleTitle.Dock = DockStyle.Top;
            panelConsoleTitle.Location = new Point(0, 0);
            panelConsoleTitle.Name = "panelConsoleTitle";
            panelConsoleTitle.Size = new Size(1100, 25);
            panelConsoleTitle.TabIndex = 0;
            // 
            // buttonCloseConsole
            // 
            buttonCloseConsole.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonCloseConsole.FlatStyle = FlatStyle.Flat;
            buttonCloseConsole.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            buttonCloseConsole.Location = new Point(1072, 2);
            buttonCloseConsole.Name = "buttonCloseConsole";
            buttonCloseConsole.Size = new Size(25, 21);
            buttonCloseConsole.TabIndex = 1;
            buttonCloseConsole.Text = "X";
            buttonCloseConsole.UseVisualStyleBackColor = true;
            buttonCloseConsole.Click += ButtonCloseConsole_Click;
            // 
            // labelConsoleTitle
            // 
            labelConsoleTitle.AutoSize = true;
            labelConsoleTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelConsoleTitle.ForeColor = SystemColors.ControlLightLight;
            labelConsoleTitle.Location = new Point(3, 5);
            labelConsoleTitle.Name = "labelConsoleTitle";
            labelConsoleTitle.Size = new Size(159, 15);
            labelConsoleTitle.TabIndex = 0;
            labelConsoleTitle.Text = "SteamCMD Console Output";
            // 
            // splitterConsole
            // 
            splitterConsole.Dock = DockStyle.Bottom;
            splitterConsole.Location = new Point(0, 353);
            splitterConsole.Name = "splitterConsole";
            splitterConsole.Size = new Size(1100, 3);
            splitterConsole.TabIndex = 4;
            splitterConsole.TabStop = false;
            splitterConsole.Visible = false;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 556);
            Controls.Add(listViewWorkshopItems);
            Controls.Add(labelNoWorkshopItems);
            Controls.Add(splitterConsole);
            Controls.Add(panelConsole);
            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;
            MinimumSize = new Size(1116, 595);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "<game> Workshop Items";
            Load += MainForm_Load;
            contextMenuStripWorkshopItems.ResumeLayout(false);
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            panelConsole.ResumeLayout(false);
            panelConsole.PerformLayout();
            panelConsoleTitle.ResumeLayout(false);
            panelConsoleTitle.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListView listViewWorkshopItems;
        private Label labelNoWorkshopItems;
        private ColumnHeader colWorkshopId;
        private ColumnHeader colWorkshopTitle;
        private MenuStrip menuStrip;
        private ToolStripMenuItem populateToolStripMenuItem;
        private ToolStripMenuItem deleteWorkshopDataToolStripMenuItem;
        private ColumnHeader colWorkshopPath;
        private ToolStripMenuItem openInExplorerToolStripMenuItem;
        private ColumnHeader colWorkshopSize;
        private ToolStripMenuItem repairInconsistenciesToolStripMenuItem;
        private ColumnHeader colWorkshopLastUpdated;
        private ColumnHeader colWorkshopUpdateStatus;
        private ToolStripMenuItem updateWorkshopItemsToolStripMenuItem;
        private ContextMenuStrip contextMenuStripWorkshopItems;
        private ToolStripMenuItem updateSelectedItemToolStripMenuItem;
        private ToolStripMenuItem openWorkshopPageToolStripMenuItem;
        private ToolStripMenuItem forceDownloadToolStripMenuItem;
        private Panel panelConsole;
        private Panel panelConsoleTitle;
        private Button buttonCloseConsole;
        private Label labelConsoleTitle;
        private TextBox textBoxConsole;
        private Splitter splitterConsole;
        private ToolStripMenuItem resetGameWorkshopToolStripMenuItem;
        private ToolStripMenuItem validateGameFilesToolStripMenuItem;
    }
}