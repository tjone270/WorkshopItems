using System.Collections;
using System.Diagnostics;
using System.Text;
using Gameloop.Vdf.Linq;

// ===============================================
// CONFIGURATION
// ===============================================
// To use this application with a different Steam game:
//   1. Change GAME_APPID to your game's Steam App ID
//   2. Recompile the application
//   3. Ensure the game is installed via Steam
// ===============================================

namespace WorkshopItems {
    public partial class MainForm : Form {
        // Change this to your game's Steam ID to manage different games
        private const int GAME_APPID = 282440; // Quake Live
        private const string PROCESS_NAME_STEAM = "steam";
        private const string PROCESS_NAME_STEAMCMD = "steamcmd";

        private Steam? _steam;
        private string? _currentLibraryFolder;
        private AppDetails? _appDetails;
        private readonly ListViewColumnSorter _columnSorter;
        private CancellationTokenSource? _cancellationTokenSource;
        private Process? _steamCmdProcess;
        private DateTime _processStartTime;

        public MainForm() {
            InitializeComponent();

            // Create and assign the column sorter
            _columnSorter = new ListViewColumnSorter();
            listViewWorkshopItems.ListViewItemSorter = _columnSorter;
        }

        private async void MainForm_Load(object sender, EventArgs e) {
            try {
                _steam = new Steam();

                if (!_steam.IsInstalled) {
                    ShowError("Could not find an installation of Steam on this system.",
                        "Steam Not Installed");
                    Application.Exit();
                    return;
                }

                // Get app details first to set up the UI
                _cancellationTokenSource = new CancellationTokenSource();
                _appDetails = await _steam.GetAppDetailsAsync(GAME_APPID, _cancellationTokenSource.Token);

                if (string.IsNullOrEmpty(_appDetails.Name)) {
                    _appDetails = _appDetails with { Name = $"App {GAME_APPID}" };
                }

                // Set form title
                Text = $"{_appDetails.Name} Workshop Items - v{Application.ProductVersion}";

                // Set form icon if available
                if (_appDetails.IconData != null && _appDetails.IconData.Length > 0) {
                    try {
                        using var ms = new MemoryStream(_appDetails.IconData);
                        using var bitmap = new Bitmap(ms);

                        // Convert to icon
                        IntPtr hIcon = bitmap.GetHicon();
                        Icon = Icon.FromHandle(hIcon);

                        // We should destroy the icon handle when done, but since we're setting it as the form icon,
                        // the form will handle it
                    } catch {
                        // If icon conversion fails, continue with default icon
                    }
                }

                // Check if Steam or SteamCMD is running
                if (!await CheckAndHandleRunningProcessesAsync()) {
                    Application.Exit();
                    return;
                }

                await RefreshAsync(false);
            } catch (SteamNotFoundException ex) {
                ShowError($"Steam installation error: {ex.Message}", "Steam Not Found");
                Application.Exit();
            } catch (Exception ex) {
                ShowError($"Unexpected error during initialisation: {ex.Message}", "Initialisation Error");
                Application.Exit();
            }
        }

        private static async Task<bool> CheckAndHandleRunningProcessesAsync() {
            var runningProcesses = GetRunningProcesses();

            if (runningProcesses.Count == 0) {
                return true;
            }

            var message = BuildRunningProcessesMessage(runningProcesses);
            var result = MessageBox.Show(
                message,
                $"{Application.ProductName} - Applications Running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes) {
                return await CloseProcessesAsync(runningProcesses);
            }

            MessageBox.Show(
                "Please close Steam manually, then restart this application.",
                $"{Application.ProductName} - Manual Close Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return false;
        }

        private static List<(string Name, Process[] Processes)> GetRunningProcesses() {
            var processes = new List<(string, Process[])>();

            var steamProcesses = Process.GetProcessesByName(PROCESS_NAME_STEAM);
            if (steamProcesses.Length > 0) {
                processes.Add(("Steam", steamProcesses));
            }

            var steamCmdProcesses = Process.GetProcessesByName(PROCESS_NAME_STEAMCMD);
            if (steamCmdProcesses.Length > 0) {
                processes.Add(("SteamCMD", steamCmdProcesses));
            }

            return processes;
        }

        private static string BuildRunningProcessesMessage(List<(string Name, Process[] Processes)> runningProcesses) {
            var message = new StringBuilder();
            message.AppendLine("The following applications are currently running:");
            message.AppendLine();

            foreach (var (name, _) in runningProcesses) {
                message.AppendLine($"    • {name}");
            }

            message.AppendLine();
            message.AppendLine("These applications must be closed before modifying workshop data.");
            message.AppendLine();
            message.AppendLine("Would you like to close them automatically?");

            return message.ToString();
        }

        private static async Task<bool> CloseProcessesAsync(List<(string Name, Process[] Processes)> processGroups) {
            try {
                // Close SteamCMD first, then Steam
                foreach (var (name, processes) in processGroups.OrderByDescending(p => p.Name == "SteamCMD")) {
                    foreach (var process in processes) {
                        try {
                            process.CloseMainWindow();
                            if (!process.WaitForExit(5000)) {
                                process.Kill();
                            }
                        } catch {
                            // Process may have already exited
                        }
                    }
                }

                // Wait for processes to fully terminate
                await Task.Delay(2000);

                // Verify all processes are closed
                var remainingProcesses = GetRunningProcesses();
                if (remainingProcesses.Count != 0) {
                    ShowError(
                        "Failed to close Steam or SteamCMD. Please close them manually and try again.",
                        "Error");
                    return false;
                }

                return true;
            } catch (Exception ex) {
                ShowError($"Error closing applications: {ex.Message}", "Error");
                return false;
            }
        }

        private async Task RefreshAsync(bool doWorkshop = true) {
            if (_steam == null) return;

            try {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                var (appManifest, workshopManifest, libraryFolder) =
                    await _steam.GetAppManifestsAsync(GAME_APPID, _cancellationTokenSource.Token);

                _currentLibraryFolder = libraryFolder;

                if (doWorkshop) {
                    await PopulateWorkshopItemsAsync(workshopManifest, libraryFolder);
                }

                populateToolStripMenuItem.Enabled = true;
            } catch (FileNotFoundException ex) {
                ShowError(
                    $"{ex.Message}\n\n" +
                    "Perhaps Steam hasn't yet been opened since installation, or isn't signed in? Or perhaps you've moved the game to a different library and not yet re-opened it?",
                    "Error Locating File");
                Application.Exit();
            } catch (InvalidOperationException ex) {
                if (ex.Message.Contains($"App {GAME_APPID} is not installed")) {
                    await HandleGameNotInstalledAsync();
                } else {
                    ShowError(ex.Message, "Error");
                }
            } catch (OperationCanceledException) {
                // Operation was cancelled, ignore
            } catch (Exception ex) {
                ShowError($"Unexpected error: {ex.Message}", "Error");
            }
        }

        private async Task HandleGameNotInstalledAsync() {
            var gameName = _appDetails?.Name ?? $"Steam App {GAME_APPID}";
            var result = MessageBox.Show(
                $"{gameName} is not installed on this system.\n\n" +
                "Go to the Steam store page for this game?",
                $"{Application.ProductName} - Game Not Installed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes) {
                Steam.CallSteamUri($"store/{GAME_APPID}");
                await Task.Delay(1000); // Give Steam time to open
            }

            Application.Exit();
        }

        private async Task PopulateWorkshopItemsAsync(dynamic? workshopManifest, string libraryFolder) {
            if (workshopManifest == null) {
                ShowNoWorkshopItems($"No workshop items detected for {_appDetails?.Name ?? "this game"}");
                return;
            }

            var workshopItems = workshopManifest.Value["WorkshopItemsInstalled"];
            if (workshopItems == null) {
                ShowError("Workshop items manifest is malformed.", "Bad Workshop Manifest");
                return;
            }

            var items = workshopItems as VObject;
            if (items?.Count == 0) {
                ShowNoWorkshopItems("No workshop items installed");
                return;
            }

            try {
                ShowWorkshopItemsList();

                var workshopIds = Steam.GetInstalledWorkshopIds(workshopManifest);
                var workshopData = await _steam!.GetWorkshopItemsWithDetailsAsync(
                    workshopIds, workshopManifest, _cancellationTokenSource!.Token);

                await PopulateListViewAsync(workshopData, libraryFolder);
            } catch (Exception ex) {
                ShowError($"Error loading workshop items: {ex.Message}", "Error");
            }
        }

        private async Task PopulateListViewAsync(
            Dictionary<string, WorkshopItemInfo> workshopData,
            string libraryFolder) {
            listViewWorkshopItems.BeginUpdate();
            try {
                listViewWorkshopItems.Items.Clear();
                var missingDataIds = new List<string>();

                foreach (var (id, info) in workshopData) {
                    var listItem = await CreateListViewItemAsync(id, info, libraryFolder, missingDataIds);
                    listViewWorkshopItems.Items.Add(listItem);
                }

                // Check for inconsistencies
                if (missingDataIds.Count != 0) {
                    await HandleMissingWorkshopDataAsync(missingDataIds, libraryFolder);
                }
            } finally {
                listViewWorkshopItems.EndUpdate();
            }
        }

        private static async Task<ListViewItem> CreateListViewItemAsync(
            string id,
            WorkshopItemInfo info,
            string libraryFolder,
            List<string> missingDataIds) {
            var path = Steam.GetWorkshopContentPath(libraryFolder, GAME_APPID, id);
            var size = "";
            var hasMissingData = false;

            // Either directory missing or empty
            if (!Directory.Exists(path) || Directory.GetFileSystemEntries(path).Length == 0) {
                path = "Data Not Present";
                missingDataIds.Add(id);
                hasMissingData = true;
            } else {
                size = await Task.Run(() => FormatFileSize(GetDirectorySize(new DirectoryInfo(path))));
            }

            var lastUpdated = info.TimeUpdated.ToString("yyyy-MM-dd HH:mm");
            var updateStatus = info.HasUpdate ? "Update Available" : "Up to Date";

            var listItem = new ListViewItem(
            [
                id,
                info.Title,
                size,
                path,
                lastUpdated,
                updateStatus
            ]);

            // Apply formatting
            if (info.HasUpdate) {
                listItem.ForeColor = Color.Blue;
                listItem.Font = new Font(listItem.Font, FontStyle.Bold);
            } else if (hasMissingData) {
                listItem.ForeColor = Color.Red;
            }

            return listItem;
        }

        private void ShowNoWorkshopItems(string message) {
            labelNoWorkshopItems.Visible = true;
            labelNoWorkshopItems.Text = message;
            listViewWorkshopItems.Visible = false;
            HideWorkshopMenuItems();
        }

        private void ShowWorkshopItemsList() {
            labelNoWorkshopItems.Visible = false;
            listViewWorkshopItems.Visible = true;
            ShowWorkshopMenuItems();
        }

        private void ShowWorkshopMenuItems() {
            deleteWorkshopDataToolStripMenuItem.Enabled = false;
            deleteWorkshopDataToolStripMenuItem.Visible = true;
            openInExplorerToolStripMenuItem.Enabled = false;
            openInExplorerToolStripMenuItem.Visible = true;
            repairInconsistenciesToolStripMenuItem.Visible = true;
            updateWorkshopItemsToolStripMenuItem.Visible = true;
            validateGameFilesToolStripMenuItem.Visible = true;
            resetGameWorkshopToolStripMenuItem.Visible = true;
        }

        private void HideWorkshopMenuItems() {
            deleteWorkshopDataToolStripMenuItem.Visible = false;
            openInExplorerToolStripMenuItem.Visible = false;
            repairInconsistenciesToolStripMenuItem.Visible = false;
            updateWorkshopItemsToolStripMenuItem.Visible = false;
            populateToolStripMenuItem.Enabled = false;
            validateGameFilesToolStripMenuItem.Visible = false;
            resetGameWorkshopToolStripMenuItem.Visible = false;
        }

        private async void PopulateToolStripMenuItem_Click(object sender, EventArgs e) {
            var menuItem = (ToolStripMenuItem)sender;
            menuItem.Enabled = false;
            menuItem.Text = "Refresh";

            try {
                await RefreshAsync();
            } finally {
                menuItem.Enabled = true;
            }
        }

        private async void DeleteWorkshopDataToolStripMenuItem_Click(object sender, EventArgs e) {
            // Check if Steam or SteamCMD is running before proceeding
            if (!await CheckAndHandleRunningProcessesAsync()) {
                return;
            }

            var itemsToDelete = GetSelectedItemsForDeletion();
            if (itemsToDelete.Count == 0) {
                MessageBox.Show(
                    "No items with data present were selected for deletion.",
                    $"{Application.ProductName} - No Valid Selection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!ConfirmDeletion(itemsToDelete)) {
                return;
            }

            await DeleteWorkshopItemsAsync(itemsToDelete);
        }

        private List<(string Id, string Title, string Path)> GetSelectedItemsForDeletion() {
            var items = new List<(string, string, string)>();

            foreach (ListViewItem item in listViewWorkshopItems.SelectedItems) {
                if (item.SubItems[3].Text != "Data Not Present") {
                    items.Add((item.Text, item.SubItems[1].Text, item.SubItems[3].Text));
                }
            }

            return items;
        }

        private static bool ConfirmDeletion(List<(string Id, string Title, string Path)> items) {
            var message = new StringBuilder();
            message.AppendLine("Are you sure you want to delete the following workshop data:");
            message.AppendLine();

            foreach (var (id, title, _) in items) {
                message.AppendLine($"    • {title} ({id})");
            }

            message.AppendLine();
            message.AppendLine("This will also remove the items from Steam's workshop manifest.");

            var result = MessageBox.Show(
                message.ToString(),
                $"{Application.ProductName} - Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }

        private async Task DeleteWorkshopItemsAsync(List<(string Id, string Title, string Path)> items) {
            var errors = new List<string>();
            var deletedPaths = new List<string>();
            var workshopIds = items.Select(i => i.Id).ToList();

            // Delete physical files
            foreach (var (id, _, path) in items) {
                try {
                    await Task.Run(() => Directory.Delete(path, true));
                    deletedPaths.Add(path);
                } catch (Exception ex) {
                    errors.Add($"Error deleting {id}: {ex.Message}");
                }
            }

            // Update manifest
            if (deletedPaths.Count != 0 && !string.IsNullOrEmpty(_currentLibraryFolder)) {
                try {
                    await Steam.RemoveWorkshopItemsFromManifestAsync(
                        GAME_APPID,
                        _currentLibraryFolder,
                        workshopIds,
                        _cancellationTokenSource!.Token);
                } catch (Exception ex) {
                    errors.Add($"Error updating manifest: {ex.Message}");
                }
            }

            // Show results
            if (errors.Count != 0) {
                ShowError(string.Join("\n", errors), "Deletion Errors");
            } else {
                MessageBox.Show(
                    "Workshop items successfully deleted and removed from Steam manifest.\n\n" +
                    "Note: You may need to restart Steam for the changes to be fully reflected.",
                    $"{Application.ProductName} - Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            await RefreshAsync();
        }

        private void ListViewWorkshopItems_SelectedIndexChanged(object sender, EventArgs e) {
            var hasSelection = listViewWorkshopItems.SelectedItems.Count > 0;
            var hasValidSelection = false;

            if (hasSelection) {
                hasValidSelection = listViewWorkshopItems.SelectedItems
                    .Cast<ListViewItem>()
                    .Any(item => item.SubItems[3].Text != "Data Not Present");
            }

            deleteWorkshopDataToolStripMenuItem.Enabled = hasValidSelection;
            openInExplorerToolStripMenuItem.Enabled = hasValidSelection;
        }

        private void OpenInExplorerToolStripMenuItem_Click(object sender, EventArgs e) {
            foreach (ListViewItem item in listViewWorkshopItems.SelectedItems) {
                var path = item.SubItems[3].Text;
                if (path != "Data Not Present") {
                    Process.Start(new ProcessStartInfo {
                        FileName = path,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
        }

        private async void RepairInconsistenciesToolStripMenuItem_Click(object sender, EventArgs e) {
            // Check if Steam or SteamCMD is running before proceeding
            if (!await CheckAndHandleRunningProcessesAsync()) {
                return;
            }

            var missingDataIds = listViewWorkshopItems.Items
                .Cast<ListViewItem>()
                .Where(item => item.SubItems[3].Text == "Data Not Present")
                .Select(item => item.Text)
                .ToList();

            if (missingDataIds.Count == 0) {
                MessageBox.Show(
                    "No inconsistencies found. All workshop items have their data files present.",
                    $"{Application.ProductName} - No Issues Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            } else if (!string.IsNullOrEmpty(_currentLibraryFolder)) {
                await HandleMissingWorkshopDataAsync(missingDataIds, _currentLibraryFolder);
            }
        }

        private async void UpdateWorkshopItemsToolStripMenuItem_Click(object sender, EventArgs e) {
            var itemsWithUpdates = GetItemsWithUpdates();

            if (itemsWithUpdates.Count == 0) {
                MessageBox.Show(
                    "All workshop items are up to date.",
                    $"{Application.ProductName} - No Updates Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var message = $"{itemsWithUpdates.Count} workshop item(s) have updates available.\n\n" +
                "Would you like to download updates for all items with available updates?\n\n" +
                "Note: SteamCMD will download the updates in a separate window.";

            if (!ConfirmAction(message, "Updates Available")) {
                return;
            }

            await LaunchWorkshopDownloadsAsync(itemsWithUpdates);
        }

        private List<string> GetItemsWithUpdates() {
            return [.. listViewWorkshopItems.Items
                .Cast<ListViewItem>()
                .Where(item => item.SubItems[5].Text == "Update Available")
                .Select(item => item.Text)];
        }

        private async Task LaunchWorkshopDownloadsAsync(List<string> workshopIds) {
            try {
                // Ensure SteamCMD is available
                var steamCmdAvailable = await _steam!.EnsureSteamCmdAvailableAsync(_cancellationTokenSource!.Token);

                if (!steamCmdAvailable) {
                    ShowError("Failed to download or locate SteamCMD.", "SteamCMD Error");
                    return;
                }

                // Show console panel
                ShowConsolePanel();

                // Clear previous output
                textBoxConsole.Clear();
                textBoxConsole.AppendText("Starting SteamCMD for workshop downloads...\r\n\r\n");

                // Process in batches if necessary
                const int batchSize = 32;
                var batches = new List<List<string>>();

                for (int i = 0; i < workshopIds.Count; i += batchSize) {
                    batches.Add([.. workshopIds.Skip(i).Take(batchSize)]);
                }

                // Process batches
                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++) {
                    if (batchIndex > 0) {
                        textBoxConsole.AppendText($"\r\n--- Processing batch {batchIndex + 1} of {batches.Count} ---\r\n\r\n");
                    }

                    _processStartTime = DateTime.Now;

                    // Launch SteamCMD directly with redirected output
                    var steamCmdPath = _steam.GetSteamCmdExecutablePath();
                    var steamCmdArgs = $"+login anonymous " + string.Join(" ",
                        batches[batchIndex].Select(id => $"+workshop_download_item {GAME_APPID} {id}")) + " +quit";

                    _steamCmdProcess = new Process {
                        StartInfo = new ProcessStartInfo {
                            FileName = steamCmdPath,
                            Arguments = steamCmdArgs,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };

                    // Set up output handlers
                    _steamCmdProcess.OutputDataReceived += OnSteamCmdOutputReceived;
                    _steamCmdProcess.ErrorDataReceived += OnSteamCmdErrorReceived;

                    _steamCmdProcess.Start();

                    // Begin async reading
                    _steamCmdProcess.BeginOutputReadLine();
                    _steamCmdProcess.BeginErrorReadLine();

                    // Wait for this batch to complete
                    await _steamCmdProcess.WaitForExitAsync();

                    // Clean up
                    _steamCmdProcess.OutputDataReceived -= OnSteamCmdOutputReceived;
                    _steamCmdProcess.ErrorDataReceived -= OnSteamCmdErrorReceived;
                    _steamCmdProcess.Dispose();
                    _steamCmdProcess = null;
                }

                textBoxConsole.AppendText("\r\n\r\nAll downloads completed.\r\n");

                // Show completion message
                MessageBox.Show(
                    "SteamCMD has finished processing workshop downloads.\n\n" +
                    "Click 'Refresh' to update the workshop items list.",
                    $"{Application.ProductName} - Downloads Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

            } catch (Exception ex) {
                ShowError($"Error launching SteamCMD download process: {ex.Message}", "Error");
            }
        }

        private void OnSteamCmdOutputReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                BeginInvoke(new Action(() => {
                    if (textBoxConsole != null && !textBoxConsole.IsDisposed) {
                        textBoxConsole.AppendText(e.Data + "\r\n");
                        textBoxConsole.ScrollToCaret();
                    }
                }));
            }
        }

        private void OnSteamCmdErrorReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                BeginInvoke(new Action(() => {
                    if (textBoxConsole != null && !textBoxConsole.IsDisposed) {
                        textBoxConsole.AppendText("[ERROR] " + e.Data + "\r\n");
                        textBoxConsole.ScrollToCaret();
                    }
                }));
            }
        }

        private void ShowConsolePanel() {
            if (!panelConsole.Visible) {
                panelConsole.Visible = true;
                splitterConsole.Visible = true;

                // Adjust the form height if needed
                if (Height < 700) {
                    Height = 750;
                }
            }
        }

        private void HideConsolePanel(bool prompt = true) {
            // Check if process is still running
            if (prompt && _steamCmdProcess != null && !_steamCmdProcess.HasExited) {
                var result = MessageBox.Show(
                    "SteamCMD is still running. Do you want to stop it?\n\n" +
                    "Click Yes to stop the process and close the console.\n" +
                    "Click No to close the console but keep the process running.\n" +
                    "Click Cancel to keep the console open.",
                    $"{Application.ProductName} - Process Running",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel) {
                    return;
                }

                if (result == DialogResult.Yes) {
                    // Terminate the process
                    try {
                        _steamCmdProcess.Kill();
                    } catch {
                        // Process may have already exited
                    }
                    _steamCmdProcess = null;
                }
            }

            panelConsole.Visible = false;
            splitterConsole.Visible = false;

            // Clean up process reference
            if (_steamCmdProcess != null) {
                _steamCmdProcess.Dispose();
                _steamCmdProcess = null;
            }
        }

        private void ButtonCloseConsole_Click(object sender, EventArgs e) {
            HideConsolePanel(true);
        }

        private async Task HandleMissingWorkshopDataAsync(List<string> missingDataIds, string libraryFolder) {
            var message = BuildMissingDataMessage(missingDataIds);

            if (!ConfirmAction(message, "Workshop Inconsistency Detected")) {
                return;
            }

            try {
                await Steam.RemoveWorkshopItemsFromManifestAsync(
                    GAME_APPID,
                    libraryFolder,
                    missingDataIds,
                    _cancellationTokenSource!.Token);

                MessageBox.Show(
                    $"Successfully removed {missingDataIds.Count} orphaned workshop item(s) from Steam manifest.\n\n" +
                    "You may need to restart Steam for the changes to be fully reflected.",
                    $"{Application.ProductName} - Repair Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await RefreshAsync();
            } catch (Exception ex) {
                ShowError($"Error while repairing workshop manifest: {ex.Message}", "Repair Failed");
            }
        }

        private string BuildMissingDataMessage(List<string> missingDataIds) {
            var message = new StringBuilder();
            message.AppendLine("Workshop inconsistency detected!");
            message.AppendLine();
            message.AppendLine("The following workshop items are registered in Steam but their data files are missing:");
            message.AppendLine();

            var itemLookup = listViewWorkshopItems.Items
                .Cast<ListViewItem>()
                .ToDictionary(item => item.Text, item => item.SubItems[1].Text);

            foreach (var id in missingDataIds) {
                if (itemLookup.TryGetValue(id, out var title)) {
                    message.AppendLine($"    • {title} ({id})");
                }
            }

            message.AppendLine();
            message.AppendLine("Would you like to repair this inconsistency by removing these items from Steam's workshop manifest?");
            message.AppendLine();
            message.AppendLine("This will make Steam's records match the actual files on disk.");

            return message.ToString();
        }

        private void ListViewWorkshopItems_ColumnClick(object sender, ColumnClickEventArgs e) {
            // Determine if clicked column is already the column that is being sorted
            if (e.Column == _columnSorter.SortColumn) {
                // Reverse the current sort direction
                _columnSorter.Order = _columnSorter.Order == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            } else {
                // Set the column number that is to be sorted; default to ascending
                _columnSorter.SortColumn = e.Column;
                _columnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort
            listViewWorkshopItems.Sort();
        }

        private async void UpdateSelectedItemToolStripMenuItem_Click(object sender, EventArgs e) {
            if (listViewWorkshopItems.SelectedItems.Count == 0) return;

            var selectedWithUpdates = listViewWorkshopItems.SelectedItems
                .Cast<ListViewItem>()
                .Where(item => item.SubItems[5].Text == "Update Available")
                .Select(item => item.Text)
                .ToList();

            if (selectedWithUpdates.Count == 0) {
                MessageBox.Show(
                    "Selected items are already up to date.",
                    $"{Application.ProductName} - No Updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var message = $"Download updates for {selectedWithUpdates.Count} selected workshop item(s)?";
            if (ConfirmAction(message, "Update Items")) {
                await LaunchWorkshopDownloadsAsync(selectedWithUpdates);
            }
        }

        private async void OpenWorkshopPageToolStripMenuItem_Click(object sender, EventArgs e) {
            if (listViewWorkshopItems.SelectedItems.Count == 0) return;

            foreach (ListViewItem item in listViewWorkshopItems.SelectedItems) {
                Steam.LaunchWorkshopItemPage(item.Text);
                await Task.Delay(500); // Small delay between opening pages
            }
        }

        private async void ForceDownloadToolStripMenuItem_Click(object sender, EventArgs e) {
            if (listViewWorkshopItems.SelectedItems.Count == 0) return;

            var selectedIds = listViewWorkshopItems.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Text)
                .ToList();

            var message = $"Force download {selectedIds.Count} selected workshop item(s)?\n\n" +
                "This will download the items regardless of their current status.";

            if (ConfirmAction(message, "Force Download")) {
                await LaunchWorkshopDownloadsAsync(selectedIds);
            }
        }

        private static long GetDirectorySize(DirectoryInfo directory) {
            try {
                return directory.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
            } catch {
                // Handle access denied or other errors
                return 0;
            }
        }

        private static string FormatFileSize(long bytes) {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1) {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private static void ShowError(string message, string title) {
            MessageBox.Show(
                message,
                $"{Application.ProductName} - {title}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static bool ConfirmAction(string message, string title) {
            var result = MessageBox.Show(
                message,
                $"{Application.ProductName} - {title}",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }

        private async void ResetGameWorkshopToolStripMenuItem_Click(object sender, EventArgs e) {
            // Check if Steam or SteamCMD is running before proceeding
            if (!await CheckAndHandleRunningProcessesAsync()) {
                return;
            }

            if (string.IsNullOrEmpty(_currentLibraryFolder)) {
                ShowError("Library folder path is not available.", "Reset Failed");
                return;
            }

            try {
                // Get paths for workshop manifest and content folder
                var workshopManifestPath = Steam.GetWorkshopManifestPath(GAME_APPID, _currentLibraryFolder);
                var workshopContentParentPath = Steam.GetWorkshopContentPath(_currentLibraryFolder, GAME_APPID, null);

                // Check if files/folders exist
                bool manifestExists = File.Exists(workshopManifestPath);
                bool contentFolderExists = Directory.Exists(workshopContentParentPath);

                if (!manifestExists && !contentFolderExists) {
                    MessageBox.Show(
                        $"No workshop data found for {_appDetails?.Name}.",
                        $"{Application.ProductName} - No Data",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Build confirmation message
                var message = new StringBuilder();
                message.AppendLine($"WARNING: This will completely reset all workshop data for {_appDetails?.Name}.");
                message.AppendLine();
                message.AppendLine("The following will be deleted:");

                if (manifestExists) {
                    message.AppendLine($"    • Workshop manifest file");
                }

                if (contentFolderExists) {
                    message.AppendLine($"    • All workshop content files ");

                    try {
                        // Count items and size
                        var directories = Directory.GetDirectories(workshopContentParentPath);
                        var totalSize = await Task.Run(() => GetDirectorySize(new DirectoryInfo(workshopContentParentPath)));
                        message.AppendLine($"      ({directories.Length} items, {FormatFileSize(totalSize)})");
                    } catch {
                        // Ignore errors when counting
                        message.AppendLine($"      (unable to count items)");
                    }
                }

                message.AppendLine();
                message.AppendLine("Are you sure you want to proceed?");

                // Confirm with user
                var result = MessageBox.Show(
                    message.ToString(),
                    $"{Application.ProductName} - Reset Workshop Data",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes) {
                    return;
                }

                // Delete workshop manifest
                if (manifestExists) {
                    File.Delete(workshopManifestPath);
                }

                // Delete workshop content folder
                if (contentFolderExists) {
                    // Delete with progress feedback
                    await Task.Run(() => {
                        try {
                            // Delete the main content folder
                            Directory.Delete(workshopContentParentPath, true);
                        } catch (Exception ex) {
                            MessageBox.Show(
                                $"Error deleting workshop content folder: {ex.Message}",
                                $"{Application.ProductName} - Deletion Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    });

                    textBoxConsole.AppendText("\r\nWorkshop data reset completed.\r\n");
                }

                MessageBox.Show(
                    "Workshop data has been reset successfully.\n\n" +
                    $"Steam/{_appDetails?.Name} may need to be restarted to fully recognise these changes.",
                    $"{Application.ProductName} - Reset Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Refresh the UI
                await RefreshAsync();
            } catch (Exception ex) {
                ShowError($"Error resetting workshop data: {ex.Message}", "Reset Failed");
            }
        }

        private async void ValidateGameFilesToolStripMenuItem_Click(object sender, EventArgs e) {
            try {
                // Check if Steam or SteamCMD is running before proceeding
                if (!await CheckAndHandleRunningProcessesAsync()) {
                    return;
                }

                // Confirm with user
                var message = $"Validate all game files and workshop content for {_appDetails?.Name}?\n\n" +
                    "This will verify the integrity of game files and repair any corrupted or missing files.\n\n" +
                    "Note: Steam will validate all files in a separate window, and will then quit. " +
                    "This process may take some time depending on the size of the game.";

                if (!ConfirmAction(message, "Validate Files")) {
                    return;
                }

                // Launch the validation process
                try {
                    var process = _steam!.ValidateAppFiles(GAME_APPID);

                    MessageBox.Show(
                        "Steam file validation has been started.\n\n" +
                        "Steam will check and repair any corrupted or missing game files and workshop content.\n\n" +
                        "You can monitor the progress in the Steam client. Steam will automatically quit when completed.",
                        $"{Application.ProductName} - Validation Started",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Show console panel for feedback
                    await process.WaitForExitAsync();
                    // Refresh the workshop items after validation
                    await RefreshAsync();
                } catch (Exception ex) {
                    ShowError($"Failed to start validation process: {ex.Message}", "Validation Error");
                }
            } catch (Exception ex) {
                ShowError($"Error during validation: {ex.Message}", "Error");
            }
        }
    }

    public class ListViewColumnSorter : IComparer {
        private readonly CaseInsensitiveComparer _comparer = new();

        public int SortColumn { get; set; }

        public SortOrder Order { get; set; } = SortOrder.None;

        public int Compare(object? x, object? y) {
            if (x is not ListViewItem itemX || y is not ListViewItem itemY) {
                return 0;
            }

            var textX = itemX.SubItems[SortColumn].Text;
            var textY = itemY.SubItems[SortColumn].Text;

            var compareResult = SortColumn switch {
                0 => CompareAsLong(textX, textY),          // ID column
                2 => CompareFileSize(textX, textY),        // Size column
                4 => CompareAsDateTime(textX, textY),      // Date column
                _ => _comparer.Compare(textX, textY)       // Default string comparison
            };

            return Order switch {
                SortOrder.Ascending => compareResult,
                SortOrder.Descending => -compareResult,
                _ => 0
            };
        }

        private int CompareAsLong(string x, string y) {
            if (long.TryParse(x, out var longX) && long.TryParse(y, out var longY)) {
                return longX.CompareTo(longY);
            }
            return _comparer.Compare(x, y);
        }

        private int CompareAsDateTime(string x, string y) {
            if (DateTime.TryParse(x, out var dateX) && DateTime.TryParse(y, out var dateY)) {
                return dateX.CompareTo(dateY);
            }
            return _comparer.Compare(x, y);
        }

        private static int CompareFileSize(string size1, string size2) {
            var bytes1 = ParseFileSize(size1);
            var bytes2 = ParseFileSize(size2);
            return bytes1.CompareTo(bytes2);
        }

        private static double ParseFileSize(string sizeStr) {
            if (string.IsNullOrWhiteSpace(sizeStr)) return 0;

            var parts = sizeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !double.TryParse(parts[0], out var size)) {
                return 0;
            }

            return parts[1].ToUpperInvariant() switch {
                "B" => size,
                "KB" => size * 1024,
                "MB" => size * 1024 * 1024,
                "GB" => size * 1024 * 1024 * 1024,
                "TB" => size * 1024 * 1024 * 1024 * 1024,
                _ => size
            };
        }
    }
}