using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

namespace WorkshopItems {
    public record WorkshopItemInfo {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public long Size { get; init; }

        public DateTime TimeUpdated { get; init; }

        public string? Manifest { get; init; }

        public DateTime? LatestTimeUpdated { get; init; }

        public string? LatestManifest { get; init; }

        public bool HasUpdate => LatestTimeUpdated.HasValue && LatestTimeUpdated.Value > TimeUpdated;
    }
    public record AppDetails {
        public int AppId { get; init; }

        public required string Name { get; init; }

        public string? IconUrl { get; init; }

        public byte[]? IconData { get; init; }
    }

    internal readonly struct AppSearchResult {
        public bool IsInstalled { get; init; }
        public string LibraryPath { get; init; }
    }

    public sealed class Steam : IDisposable {
        private const string SteamRegistryKey32Bit = @"HKEY_LOCAL_MACHINE\Software\Wow6432Node\Valve\Steam";
        private const string SteamRegistryKey64Bit = @"HKEY_LOCAL_MACHINE\Software\Valve\Steam";
        private const string DefaultApiHost = "https://api.steampowered.com";
        private const string SteamCmdDownloadUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

        private readonly HttpClient _httpClient;
        private bool _disposed;

        public string InstallPath { get; }

        public string ApiHost { get; }

        public bool IsInstalled => !string.IsNullOrEmpty(InstallPath);

        public Steam(string apiHost = DefaultApiHost, HttpClient? httpClient = null) {
            ArgumentNullException.ThrowIfNull(apiHost);

            ApiHost = apiHost;
            _httpClient = httpClient ?? new HttpClient();

            try {
                InstallPath = GetSteamInstallPath();
            } catch (Exception ex) {
                throw new SteamNotFoundException("Failed to locate Steam installation.", ex);
            }
        }

        private static string GetSteamInstallPath() {
            // Try 32-bit registry
            var installPath = Registry.GetValue(SteamRegistryKey32Bit, "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath)) {
                return installPath;
            }

            // Try 64-bit registry
            installPath = Registry.GetValue(SteamRegistryKey64Bit, "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath)) {
                return installPath;
            }

            throw new SteamNotFoundException(
                "Steam is not installed, or the registry keys providing the Steam installation path could not be found."
            );
        }

        public async Task<(dynamic appManifest, dynamic? workshopManifest, string libraryFolderPath)>
            GetAppManifestsAsync(int appId, CancellationToken cancellationToken = default) {
            var libraryFoldersVdfPath = Path.Combine(InstallPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(libraryFoldersVdfPath)) {
                throw new FileNotFoundException($"Expected to find 'libraryfolders.vdf' at: {libraryFoldersVdfPath}");
            }

            var libraryFoldersContent = await File.ReadAllTextAsync(libraryFoldersVdfPath, cancellationToken);
            dynamic libraryFolders = VdfConvert.Deserialize(libraryFoldersContent);

            var result = FindAppInLibraries(libraryFolders, appId);

            if (!result.IsInstalled) {
                throw new InvalidOperationException($"App {appId} is not installed.");
            }

            var appManifest = await LoadAppManifestAsync(result.LibraryPath, appId, cancellationToken);
            var workshopManifest = await TryLoadWorkshopManifestAsync(result.LibraryPath, appId, cancellationToken);

            return (appManifest, workshopManifest, result.LibraryPath);
        }

        private static AppSearchResult FindAppInLibraries(dynamic libraryFolders, int appId) {
            foreach (dynamic libraryFolder in libraryFolders.Value) {
                dynamic apps = libraryFolder.Value["apps"];
                string libraryPath = libraryFolder.Value["path"].ToString();

                foreach (dynamic app in apps) {
                    if (app == null) {
                        throw new InvalidOperationException("Error parsing 'libraryfolders.vdf': null app entry found.");
                    }

                    if (Convert.ToInt32(app.Key) == appId) {
                        return new AppSearchResult {
                            IsInstalled = true,
                            LibraryPath = libraryPath
                        };
                    }
                }
            }

            return new AppSearchResult {
                IsInstalled = false,
                LibraryPath = string.Empty
            };
        }

        private static async Task<dynamic> LoadAppManifestAsync(string libraryPath, int appId, CancellationToken cancellationToken) {
            var appManifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{appId}.acf");

            if (!File.Exists(appManifestPath)) {
                throw new FileNotFoundException($"App manifest not found at: {appManifestPath}");
            }

            var content = await File.ReadAllTextAsync(appManifestPath, cancellationToken);
            return VdfConvert.Deserialize(content);
        }

        private static async Task<dynamic?> TryLoadWorkshopManifestAsync(string libraryPath, int appId, CancellationToken cancellationToken) {
            var workshopManifestPath = Path.Combine(libraryPath, "steamapps", "workshop", $"appworkshop_{appId}.acf");

            if (!File.Exists(workshopManifestPath)) {
                return null;
            }

            try {
                var content = await File.ReadAllTextAsync(workshopManifestPath, cancellationToken);
                dynamic manifest = VdfConvert.Deserialize(content);

                if (Convert.ToInt32(manifest.Value["appid"].ToString()) != appId) {
                    throw new InvalidOperationException(
                        $"Workshop manifest appId ({manifest.Value["appid"]}) does not match target appId ({appId}).");
                }

                return manifest;
            } catch (Exception ex) when (ex is not InvalidOperationException) {
                // Log error but don't fail - workshop content is optional
                return null;
            }
        }

        public async Task<AppDetails> GetAppDetailsAsync(int appId, CancellationToken cancellationToken = default) {
            var url = $"{ApiHost}/ICommunityService/GetApps/v1/?appids[0]={appId}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonNode.Parse(content);
            var appNode = root?["response"]?["apps"]?[0];

            if (appNode == null) {
                return new AppDetails { AppId = appId, Name = "Unknown Game" };
            }

            var iconHash = appNode["icon"]?.ToString();
            string? iconUrl = null;
            byte[]? iconData = null;

            if (!string.IsNullOrEmpty(iconHash)) {
                iconUrl = $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{appId}/{iconHash}.jpg";

                // Download the icon
                try {
                    using var iconResponse = await _httpClient.GetAsync(iconUrl, cancellationToken);
                    if (iconResponse.IsSuccessStatusCode) {
                        iconData = await iconResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                    }
                } catch {
                    // If icon download fails, continue without it
                }
            }

            return new AppDetails {
                AppId = appNode["appid"]?.GetValue<int>() ?? appId,
                Name = appNode["name"]?.ToString() ?? "Unknown Game",
                IconUrl = iconUrl,
                IconData = iconData
            };
        }

        public async Task<Dictionary<string, string>> GetWorkshopItemNamesAsync(
            IEnumerable<string> workshopIds,
            CancellationToken cancellationToken = default) {
            var idList = workshopIds.ToList();
            if (idList.Count == 0) {
                return [];
            }

            var formContent = new FormUrlEncodedContent(BuildWorkshopDetailsParameters(idList));

            using var response = await _httpClient.PostAsync(
                $"{ApiHost}/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                formContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseWorkshopDetailsResponse(content);
        }

        private static List<KeyValuePair<string, string>> BuildWorkshopDetailsParameters(List<string> workshopIds) {
            var parameters = new List<KeyValuePair<string, string>>
            {
                new("itemcount", workshopIds.Count.ToString())
            };

            for (int i = 0; i < workshopIds.Count; i++) {
                parameters.Add(new($"publishedfileids[{i}]", workshopIds[i]));
            }

            return parameters;
        }

        private static Dictionary<string, string> ParseWorkshopDetailsResponse(string content) {
            var result = new Dictionary<string, string>();
            var root = JsonNode.Parse(content);
            var fileDetails = root?["response"]?["publishedfiledetails"]?.AsArray();

            if (fileDetails == null) return result;

            foreach (var detail in fileDetails) {
                var id = detail?["publishedfileid"]?.ToString();
                var title = detail?["title"]?.ToString();

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title)) {
                    result[id] = title;
                }
            }

            return result;
        }

        public async Task<Dictionary<string, WorkshopItemInfo>> GetWorkshopItemsWithDetailsAsync(
            IEnumerable<string> workshopIds,
            dynamic? workshopManifest,
            CancellationToken cancellationToken = default) {
            var idList = workshopIds.ToList();
            var basicInfo = await GetWorkshopItemNamesAsync(idList, cancellationToken);
            var apiInfo = await GetWorkshopItemDetailsFromAPIAsync(idList, cancellationToken);

            var detailedInfo = new Dictionary<string, WorkshopItemInfo>();
            var workshopItems = workshopManifest?.Value["WorkshopItemsInstalled"];
            var workshopDetails = workshopManifest?.Value["WorkshopItemDetails"];

            foreach (var (id, title) in basicInfo) {
                var info = new WorkshopItemInfo {
                    Id = id,
                    Title = title
                };

                info = PopulateFromManifest(info, id, workshopItems, workshopDetails);
                info = UpdateFromAPI(info, id, apiInfo);

                detailedInfo[id] = info;
            }

            return detailedInfo;
        }

        private static WorkshopItemInfo PopulateFromManifest(
            WorkshopItemInfo info,
            string id,
            dynamic? workshopItems,
            dynamic? workshopDetails) {
            if (workshopItems?[id] != null) {
                dynamic item = workshopItems[id];
                info = info with {
                    Size = long.Parse(item["size"].ToString()),
                    TimeUpdated = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item["timeupdated"].ToString())).DateTime,
                    Manifest = item["manifest"]?.ToString()
                };
            }

            if (workshopDetails?[id] != null) {
                dynamic detail = workshopDetails[id];

                if (detail["latest_timeupdated"] != null) {
                    info = info with {
                        LatestTimeUpdated = DateTimeOffset.FromUnixTimeSeconds(
                            long.Parse(detail["latest_timeupdated"].ToString())).DateTime
                    };
                }

                if (detail["latest_manifest"] != null) {
                    info = info with { LatestManifest = detail["latest_manifest"].ToString() };
                }
            }

            return info;
        }

        private static WorkshopItemInfo UpdateFromAPI(
            WorkshopItemInfo info,
            string id,
            Dictionary<string, (string title, DateTime timeUpdated)> apiInfo) {
            if (apiInfo.TryGetValue(id, out var apiItem) && apiItem.timeUpdated > info.TimeUpdated) {
                info = info with { LatestTimeUpdated = apiItem.timeUpdated };
            }

            return info;
        }

        private async Task<Dictionary<string, (string title, DateTime timeUpdated)>> GetWorkshopItemDetailsFromAPIAsync(
            List<string> workshopIds,
            CancellationToken cancellationToken) {
            var formContent = new FormUrlEncodedContent(BuildWorkshopDetailsParameters(workshopIds));

            using var response = await _httpClient.PostAsync(
                $"{ApiHost}/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                formContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseWorkshopDetailsForUpdates(content);
        }

        private static Dictionary<string, (string title, DateTime timeUpdated)> ParseWorkshopDetailsForUpdates(string content) {
            var result = new Dictionary<string, (string, DateTime)>();
            var root = JsonNode.Parse(content);
            var fileDetails = root?["response"]?["publishedfiledetails"]?.AsArray();

            if (fileDetails == null) return result;

            foreach (var detail in fileDetails) {
                var id = detail?["publishedfileid"]?.ToString();
                if (string.IsNullOrEmpty(id)) continue;

                var title = detail?["title"]?.ToString() ?? "Unknown";
                var timeUpdatedStr = detail?["time_updated"]?.ToString();

                if (!string.IsNullOrEmpty(timeUpdatedStr) && long.TryParse(timeUpdatedStr, out var timeUpdated)) {
                    var updateTime = DateTimeOffset.FromUnixTimeSeconds(timeUpdated).DateTime;
                    result[id] = (title, updateTime);
                }
            }

            return result;
        }

        public static string GetWorkshopContentPath(string libraryFolderPath, int appId, string? workshopId) {
            ArgumentException.ThrowIfNullOrEmpty(libraryFolderPath);
            
            if (string.IsNullOrEmpty(workshopId)) {
                return Path.Combine(libraryFolderPath, "steamapps", "workshop", "content", appId.ToString());
            }

            return Path.Combine(libraryFolderPath, "steamapps", "workshop", "content", appId.ToString(), workshopId);
        }

        public string GetSteamExecutablePath() => Path.Combine(InstallPath, "steam.exe");

        public string GetSteamCmdExecutablePath() => Path.Combine(InstallPath, "steamcmd.exe");

        public async Task<bool> EnsureSteamCmdAvailableAsync(CancellationToken cancellationToken = default) {
            var steamCmdPath = GetSteamCmdExecutablePath();

            if (File.Exists(steamCmdPath)) {
                return true;
            }

            try {
                // Download SteamCMD ZIP
                using var response = await _httpClient.GetAsync(SteamCmdDownloadUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var zipPath = Path.Combine(InstallPath, "steamcmd.zip");

                // Save ZIP file
                await using (var fileStream = File.Create(zipPath)) {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }

                // Extract ZIP file
                ZipFile.ExtractToDirectory(zipPath, InstallPath, true);

                // Clean up ZIP file
                File.Delete(zipPath);

                return File.Exists(steamCmdPath);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to download or extract SteamCMD: {ex.Message}", ex);
            }
        }

        public static List<string> GetInstalledWorkshopIds(dynamic? workshopManifest) {
            var workshopIds = new List<string>();

            if (workshopManifest?.Value["WorkshopItemsInstalled"] is { } workshopItems) {
                foreach (dynamic item in workshopItems) {
                    workshopIds.Add(item.Key.ToString());
                }
            }

            return workshopIds;
        }

        public static async Task RemoveWorkshopItemsFromManifestAsync(
            int appId,
            string libraryFolderPath,
            IEnumerable<string> workshopIdsToRemove,
            CancellationToken cancellationToken = default) {
            var workshopManifestPath = GetWorkshopManifestPath(appId, libraryFolderPath);

            if (!File.Exists(workshopManifestPath)) {
                throw new FileNotFoundException($"Workshop manifest not found at: {workshopManifestPath}");
            }

            var backupPath = $"{workshopManifestPath}.backup";

            // Create backup
            await using (var sourceStream = File.OpenRead(workshopManifestPath))
            await using (var backupStream = File.Create(backupPath)) {
                await sourceStream.CopyToAsync(backupStream, cancellationToken);
            }

            try {
                await ModifyWorkshopManifestAsync(workshopManifestPath, workshopIdsToRemove, cancellationToken);

                // Delete backup on success
                File.Delete(backupPath);
            } catch {
                // Restore backup on error
                if (File.Exists(backupPath)) {
                    File.Copy(backupPath, workshopManifestPath, true);
                    File.Delete(backupPath);
                }
                throw;
            }
        }

        private static async Task ModifyWorkshopManifestAsync(
            string manifestPath,
            IEnumerable<string> workshopIdsToRemove,
            CancellationToken cancellationToken) {
            var manifestContent = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            dynamic workshopManifest = VdfConvert.Deserialize(manifestContent);

            var idsToRemove = workshopIdsToRemove.ToHashSet();

            // Remove from WorkshopItemsInstalled
            if (workshopManifest.Value["WorkshopItemsInstalled"] is VObject itemsObject) {
                foreach (var id in idsToRemove.Where(itemsObject.ContainsKey)) {
                    itemsObject.Remove(id);
                }
            }

            // Remove from WorkshopItemDetails
            if (workshopManifest.Value["WorkshopItemDetails"] is VObject detailsObject) {
                foreach (var id in idsToRemove.Where(detailsObject.ContainsKey)) {
                    detailsObject.Remove(id);
                }
            }

            var updatedManifest = VdfConvert.Serialize(workshopManifest);
            await File.WriteAllTextAsync(manifestPath, updatedManifest, cancellationToken);
        }

        public static string GetWorkshopManifestPath(int appId, string libraryFolderPath) {
            ArgumentException.ThrowIfNullOrEmpty(libraryFolderPath);
            return Path.Combine(libraryFolderPath, "steamapps", "workshop", $"appworkshop_{appId}.acf");
        }

        public Process DownloadWorkshopItem(int appId, string workshopId) {
            ArgumentException.ThrowIfNullOrEmpty(workshopId);

            return StartSteamCmdProcess($"+login anonymous +workshop_download_item {appId} {workshopId.Trim()} +quit");
        }

        public Process DownloadWorkshopItems(int appId, List<string> workshopIds) {
            if (workshopIds.Count == 0) {
                throw new ArgumentException("No workshop IDs provided.", nameof(workshopIds));
            }

            var arguments = "+login anonymous " + string.Join(" ",
                workshopIds.Select(id => $"+workshop_download_item {appId} {id.Trim()}")) + " +quit";

            return StartSteamCmdProcess(arguments);
        }

        public Process ValidateAppFiles(int appId) {
            return StartSteamProcess($"+app_update {appId} validate +quit");
        }

        private Process StartSteamProcess(string arguments, bool silent = false) {
            if (silent) {
                arguments = $"-silent {arguments}";
            }

            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = GetSteamExecutablePath(),
                    Arguments = $"-console {arguments}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            return process;
        }

        private Process StartSteamCmdProcess(string arguments) {
            var steamCmdPath = GetSteamCmdExecutablePath();

            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = steamCmdPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            try {
                process.Start();
                return process;
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to start SteamCMD: {ex.Message}", ex);
            }
        }

        public static void CallSteamUri(string uri) {
            ArgumentException.ThrowIfNullOrEmpty(uri);

            Process.Start(new ProcessStartInfo {
                FileName = $"steam://{uri}",
                UseShellExecute = true
            });
        }

        public static void LaunchWorkshopItemPage(string workshopId) {
            ArgumentException.ThrowIfNullOrEmpty(workshopId);

            var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}";
            Process.Start(new ProcessStartInfo {
                FileName = url,
                UseShellExecute = true
            });
        }

        public static bool IsSteamRunning() {
            return Process.GetProcessesByName("steam").Length > 0;
        }

        /// <summary>
        /// Starts the Steam client if it's not already running.
        /// </summary>
        public void StartSteam() {
            if (!IsSteamRunning() && IsInstalled) {
                Process.Start(new ProcessStartInfo {
                    FileName = GetSteamExecutablePath(),
                    UseShellExecute = true
                });
            }
        }

        public void Dispose() {
            if (_disposed) return;

            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    public class SteamNotFoundException : Exception {
        public SteamNotFoundException() : base() { }

        public SteamNotFoundException(string message) : base(message) { }

        public SteamNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}