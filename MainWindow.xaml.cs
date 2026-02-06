using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using U_Wii_X_Fusion.Core.GameIdentification;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Core.Settings;
using U_Wii_X_Fusion.Core.Update;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion
{
    /// <summary>用于目标分区下拉框的项</summary>
    public sealed class DriveItem
    {
        public string DisplayName { get; set; }
        public string RootPath { get; set; }
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private WiiGameDatabase _wiiDatabase;
        // 三种来源各自的游戏列表
        private readonly List<GameInfo> _directoryGames = new List<GameInfo>();
        private readonly List<GameInfo> _disk1Games = new List<GameInfo>();
        private readonly List<GameInfo> _disk2Games = new List<GameInfo>();
        private List<GameInfo> _scannedGames;  // 当前正在显示/操作的 Wii/NGC 游戏列表（引用上述三者之一）
        private string _coverPath;
        private readonly WiiGameIdentifier _wiiIdentifier = new WiiGameIdentifier();
        private readonly NgcGameIdentifier _ngcIdentifier = new NgcGameIdentifier();
        private enum GameListSource { Directory, Disk1, Disk2 }
        private GameListSource _currentListSource = GameListSource.Directory;
        private string _disk1Root;
        private string _disk2Root;

        public MainWindow()
        {
            InitializeComponent();
            InitializeHeader();
            // 默认显示目录来源的游戏列表
            _scannedGames = _directoryGames;
            dgGames.ItemsSource = _scannedGames;
            InitializeDatabase();
            SetupEventHandlers();
            LoadSettings();
            LoadCoverPath();
            LoadDrives();
            UpdateGameCount(); // 初始化时显示 0
            UpdateWiiListStatus();
            var statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            statusTimer.Tick += (s, _) => UpdateWiiListStatus();
            statusTimer.Start();
        }

        private void InitializeHeader()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
            if (txtHeaderVersion != null)
                txtHeaderVersion.Text = $"v{version}";

            Title = $"U-Wii-X Fusion v{version}";
        }

        private void InitializeDatabase()
        {
            try
            {
                _wiiDatabase = new WiiGameDatabase();
                _wiiDatabase.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化数据库时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupEventHandlers()
        {
            btnSaveSettings.Click += BtnSaveSettings_Click;
            btnBrowseCoverPath.Click += BtnBrowseCoverPath_Click;
            btnBrowseGamePath.Click += BtnBrowseGamePath_Click;
            btnBrowseDatabasePath.Click += BtnBrowseDatabasePath_Click;
            btnCheckUpdate.Click += BtnCheckUpdate_Click;
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            var updateService = new UpdateService("wei134102", "U-Wii-X-Fusion");
            string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
            var updateWindow = new UpdateWindow(updateService, currentVersion) { Owner = this };
            updateWindow.ShowDialog();
        }

        /// <summary>加载可用的磁盘分区（移动硬盘、U盘、其他分区）供选择</summary>
        private void LoadDrives()
        {
            var drives = new List<DriveItem>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    try
                    {
                        string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "本地磁盘" : drive.VolumeLabel;
                        string sizeStr = "未知大小";
                        if (drive.TotalSize > 0)
                        {
                            double gb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                            sizeStr = $"{gb:F1} GB";
                        }
                        string typeStr = drive.DriveType == DriveType.Removable ? "可移动" :
                                         drive.DriveType == DriveType.Fixed ? "本地" : drive.DriveType.ToString();
                        drives.Add(new DriveItem
                        {
                            DisplayName = $"{drive.Name} {label} ({sizeStr}) [{typeStr}]",
                            RootPath = drive.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        });
                    }
                    catch { /* 忽略无权限的盘 */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("无法枚举驱动器: " + ex.Message);
            }
            if (cboDiskDrive != null)
            {
                cboDiskDrive.ItemsSource = drives;
                // 不自动选中盘符，避免默认选到 C 盘导致误扫描
                cboDiskDrive.SelectedIndex = -1;
            }
        }

        private void CboDiskDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var di = cboDiskDrive.SelectedItem as DriveItem;
            if (di == null) return;
            if (_currentListSource == GameListSource.Disk1)
            {
                _disk1Root = di.RootPath;
                // 选中盘符后立即扫描该磁盘的 wbfs/games 目录
                ScanGamesFromDiskRoot(_disk1Root);
            }
            else if (_currentListSource == GameListSource.Disk2)
            {
                _disk2Root = di.RootPath;
                ScanGamesFromDiskRoot(_disk2Root);
            }
        }

        /// <summary>更新游戏数量统计显示</summary>
        private void UpdateGameCount()
        {
            int wiiCount = _scannedGames.Count(g => g.Platform == "Wii");
            int ngcCount = _scannedGames.Count(g => IsNgcGame(g));
            txtGameCount.Text = $"Wii: {wiiCount}  |  NGC: {ngcCount}";
        }

        private static bool IsNgcGame(GameInfo g)
        {
            if (g == null) return false;
            // 兼容数据库/识别器可能写成 "NGC" 或 "GameCube"
            return string.Equals(g.Platform, "NGC", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(g.PlatformType, "NGC", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(g.Platform, "GameCube", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(g.PlatformType, "GameCube", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double n = bytes;
            while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
            return $"{n:F2} {u[i]}";
        }

        private void UpdateWiiListStatus()
        {
            int selCount = _scannedGames.Count(g => g.IsSelected);
            long selSize = _scannedGames.Where(g => g.IsSelected).Sum(g => g.Size);
            string driveInfo = "";
            try
            {
                string rootPath = null;
                if (_currentListSource == GameListSource.Disk1)
                    rootPath = _disk1Root;
                else if (_currentListSource == GameListSource.Disk2)
                    rootPath = _disk2Root;

                if (!string.IsNullOrWhiteSpace(rootPath))
                {
                    string root = Path.GetPathRoot(rootPath);
                    if (!string.IsNullOrEmpty(root))
                    {
                        var d = new DriveInfo(root);
                        if (d.IsReady)
                            driveInfo = $"  目标磁盘({root.TrimEnd('\\')}): 剩余 {FormatSize(d.TotalFreeSpace)} | 总 {FormatSize(d.TotalSize)}";
                    }
                }
            }
            catch
            {
                driveInfo = "";
            }

            txtWiiListStatus.Text = $"选中: {selCount} 个，共 {FormatSize(selSize)}{driveInfo}";
        }

        #region Wii 扫描与拷贝

        private void ScanAndAddGamesFromDirectory(string path, bool clearExisting, List<GameInfo> targetList)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("所选目录不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (clearExisting)
            {
                targetList.Clear();
            }

            var splitExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wbfs", ".wbf1", ".wbf2", ".wbf3", ".wbf4" };
            try
            {
                var allFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                // 1) 分组：同一目录下 同主名的 .wbfs/.wbf1~.wbf4 视为一个分割游戏
                var wbfsGroups = new Dictionary<string, List<(string filePath, long size)>>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in allFiles)
                {
                    string ext = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(ext) || !splitExts.Contains(ext)) continue;
                    try
                    {
                        string dir = Path.GetDirectoryName(file);
                        string baseName = Path.GetFileNameWithoutExtension(file);
                        string key = dir + "\0" + baseName;
                        if (!wbfsGroups.TryGetValue(key, out var list))
                        {
                            list = new List<(string, long)>();
                            wbfsGroups[key] = list;
                        }
                        long len = new FileInfo(file).Length;
                        list.Add((file, len));
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"跳过 {file}: {ex.Message}"); }
                }

                foreach (var kv in wbfsGroups)
                {
                    var list = kv.Value;
                    string wbfsPath = list.FirstOrDefault(f => f.filePath.EndsWith(".wbfs", StringComparison.OrdinalIgnoreCase)).filePath;
                    if (string.IsNullOrEmpty(wbfsPath)) continue; // 必须有 .wbfs 主文件
                    long totalSize = list.Sum(f => f.size);
                    string baseName = Path.GetFileNameWithoutExtension(wbfsPath);

                    // 对 .wbfs 分割游戏，仍然通过 Wii 标识器识别基础信息，然后汇总大小。
                    var game = _wiiIdentifier.IdentifyGame(wbfsPath);
                    if (game == null) continue;
                    game.Size = totalSize;

                    // 如果标识器已经从文件内容中解析出了 GameId，则优先使用；
                    // 否则回退为文件基名（兼容老的“ID.wbfs / xxx [ID].wbfs”命名）。
                    string idForDb = !string.IsNullOrWhiteSpace(game.GameId) ? game.GameId : baseName;
                    if (string.IsNullOrWhiteSpace(game.GameId))
                        game.GameId = idForDb;

                    EnrichGameFromDatabase(game, idForDb);
                    targetList.Add(game);
                }

                // 2) 单文件游戏：仅 .iso / .wad / .gcm（.wbfs 已在上面分组中处理，含单独一个 .wbfs 的情况）
                foreach (string file in allFiles)
                {
                    string ext = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(ext)) continue;
                    if (!ext.Equals(".iso", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".wad", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".gcm", StringComparison.OrdinalIgnoreCase))
                        continue;

                    GameInfo game = null;
                    if (_wiiIdentifier.IsSupportedFormat(file))
                        game = _wiiIdentifier.IdentifyGame(file);
                    else if (_ngcIdentifier.IsSupportedFormat(file))
                        game = _ngcIdentifier.IdentifyGame(file);

                    if (game != null)
                    {
                        string baseName = Path.GetFileNameWithoutExtension(file);

                        // 若标识器从光盘头中读出了 GameId，则优先用 GameId 去数据库匹配；
                        // 否则仍然回退为文件名基名。
                        string idForDb = !string.IsNullOrWhiteSpace(game.GameId) ? game.GameId : baseName;
                        if (string.IsNullOrWhiteSpace(game.GameId))
                            game.GameId = idForDb;

                        EnrichGameFromDatabase(game, idForDb);
                        targetList.Add(game);
                    }
                }

                // NGC 多盘布局（game.iso + disc2.iso + sys 文件夹）后处理：合并为一个游戏并汇总大小
                PostProcessNgcMultiDisc(targetList);

                if (_scannedGames == targetList)
                {
                    dgGames.Items.Refresh();
                    UpdateGameCount();
                    UpdateWiiListStatus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 针对 NGC Dolphin 布局，将同一目录下的 game.iso / disc2.iso / sys 视为一个游戏：
        /// - 使用 game.iso 对应的 GameInfo 作为主条目；
        /// - 将 disc2.iso 和 sys 文件夹的大小累加到主条目的 Size；
        /// - 删除 disc2.iso 对应的 GameInfo（不再单独显示为一条游戏）。
        /// 不再依赖 Platform/PlatformType 标记，仅依赖目录结构，避免识别阶段将 ISO 误标为 Wii 导致漏算。
        /// </summary>
        private void PostProcessNgcMultiDisc(List<GameInfo> list)
        {
            if (list == null || list.Count == 0) return;

            // 按目录分组所有有路径的条目
            var dirGroups = list
                .Where(g => !string.IsNullOrEmpty(g.Path))
                .GroupBy(g => Path.GetDirectoryName(g.Path) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toRemove = new HashSet<GameInfo>();

            foreach (var group in dirGroups)
            {
                string dir = group.Key;
                if (string.IsNullOrEmpty(dir)) continue;

                // 目录下必须存在 game.iso，才按 NGC Dolphin 游戏处理
                var primary = group.FirstOrDefault(g =>
                    string.Equals(Path.GetFileName(g.Path), "game.iso", StringComparison.OrdinalIgnoreCase));
                if (primary == null) continue;

                long extraSize = 0;

                // disc2.iso 额外镜像：优先通过 GameInfo 找到，其次直接看文件
                foreach (var g in group)
                {
                    if (ReferenceEquals(g, primary)) continue;
                    var fileName = Path.GetFileName(g.Path);
                    if (string.Equals(fileName, "disc2.iso", StringComparison.OrdinalIgnoreCase))
                    {
                        try { extraSize += new FileInfo(g.Path).Length; } catch { }
                        toRemove.Add(g);
                    }
                }

                // 如果没有单独的 GameInfo 记录，也尝试直接按文件夹扫一遍 disc2.iso
                string disc2Path = Path.Combine(dir, "disc2.iso");
                if (File.Exists(disc2Path) && !group.Any(g => string.Equals(g.Path, disc2Path, StringComparison.OrdinalIgnoreCase)))
                {
                    try { extraSize += new FileInfo(disc2Path).Length; } catch { }
                }

                // sys 文件夹大小
                try
                {
                    string sysDir = Path.Combine(dir, "sys");
                    if (Directory.Exists(sysDir))
                    {
                        foreach (var f in Directory.GetFiles(sysDir, "*", SearchOption.AllDirectories))
                        {
                            try { extraSize += new FileInfo(f).Length; }
                            catch { /* ignore individual file errors */ }
                        }
                    }
                }
                catch { /* ignore */ }

                primary.Size += extraSize;
            }

            if (toRemove.Count > 0)
            {
                list.RemoveAll(g => toRemove.Contains(g));
            }
        }

        private void BtnAddSource_Click(object sender, RoutedEventArgs e)
        {
            // 仅在“目录”模式下允许添加文件/目录
            _currentListSource = GameListSource.Directory;
            btnAddSource.ContextMenu?.IsOpen.Equals(true);
            if (btnAddSource.ContextMenu != null)
            {
                btnAddSource.ContextMenu.PlacementTarget = btnAddSource;
                btnAddSource.ContextMenu.IsOpen = true;
            }
        }

        private void MenuAddDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择包含 Wii/NGC 游戏的目录";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ScanAndAddGamesFromDirectory(dlg.SelectedPath, clearExisting: false, _directoryGames);
                }
            }
        }

        private void MenuAddFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "选择 Wii/NGC 游戏文件",
                Filter = "游戏镜像 (*.iso;*.wbfs;*.wad;*.gcm)|*.iso;*.wbfs;*.wad;*.gcm|所有文件 (*.*)|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() != true) return;

            var files = ofd.FileNames;
            if (files == null || files.Length == 0) return;

            // 简单复用目录扫描逻辑：对所选文件所在目录进行补充扫描（不清空）
            foreach (var group in files.GroupBy(f => Path.GetDirectoryName(f)))
            {
                string dir = group.Key;
                if (Directory.Exists(dir))
                {
                    ScanAndAddGamesFromDirectory(dir, clearExisting: false, _directoryGames);
                }
            }
        }

        private void EnrichGameFromDatabase(GameInfo game, string baseName)
        {
            var dbGame = _wiiDatabase?.GetGame(baseName);
            if (dbGame == null && baseName.IndexOfAny(new[] { ' ', '[' }) > 0)
            {
                string extractedId = baseName.Split(new[] { ' ', '[' }, 2)[0].Trim();
                dbGame = _wiiDatabase?.GetGame(extractedId);
                if (dbGame != null)
                    game.GameId = extractedId;
            }
            if (dbGame != null)
            {
                // 基础标题信息
                game.Title = dbGame.Title ?? baseName;
                game.ChineseTitle = dbGame.ChineseTitle ?? string.Empty;

                // 平台及类型
                if (!string.IsNullOrWhiteSpace(dbGame.Platform))
                    game.Platform = dbGame.Platform;
                if (!string.IsNullOrWhiteSpace(dbGame.PlatformType))
                    game.PlatformType = dbGame.PlatformType;

                // 详细信息：区域 / 人数 / 厂商
                game.Region = dbGame.Region;
                game.Players = dbGame.Players;
                game.Publisher = dbGame.Publisher;
                game.Developer = dbGame.Developer;

                // 控制器 / 类型 / 语言
                game.Controllers = new List<string>(dbGame.Controllers ?? new List<string>());
                game.Genres = new List<string>(dbGame.Genres ?? new List<string>());
                game.Languages = new List<string>(dbGame.Languages ?? new List<string>());

                // 简介（synopsis）
                game.Synopsis = dbGame.Synopsis;
            }
            else
            {
                game.Title = baseName;
                game.ChineseTitle = string.Empty;
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in _scannedGames) g.IsSelected = true;
            dgGames.Items.Refresh();
            UpdateWiiListStatus();
        }

        private void BtnInvertSelect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in _scannedGames) g.IsSelected = !g.IsSelected;
            dgGames.Items.Refresh();
            UpdateWiiListStatus();
        }

        private void BtnClearSelect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in _scannedGames) g.IsSelected = false;
            dgGames.Items.Refresh();
            UpdateWiiListStatus();
        }

        private void BtnListSourceDirectory_Click(object sender, RoutedEventArgs e)
        {
            _currentListSource = GameListSource.Directory;
            // 目录模式：显示“添加”按钮，隐藏磁盘下拉框
            if (btnAddSource != null) btnAddSource.Visibility = Visibility.Visible;
            if (cboDiskDrive != null) cboDiskDrive.Visibility = Visibility.Collapsed;

            // 切换到目录来源的列表
            _scannedGames = _directoryGames;
            dgGames.ItemsSource = _scannedGames;
            dgGames.Items.Refresh();
            UpdateGameCount();
            UpdateWiiListStatus();
        }

        private void BtnListSourceDisk1_Click(object sender, RoutedEventArgs e)
        {
            _currentListSource = GameListSource.Disk1;
            // 磁盘模式：隐藏“添加”按钮，显示磁盘下拉框
            if (btnAddSource != null) btnAddSource.Visibility = Visibility.Collapsed;
            if (cboDiskDrive != null)
            {
                cboDiskDrive.Visibility = Visibility.Visible;
                LoadDrives();
                // 不再从配置恢复盘符，用户每次自行选择
                RestoreDiskDriveSelection(GameListSource.Disk1);
            }

            // 切换到磁盘1来源的列表
            _scannedGames = _disk1Games;
            dgGames.ItemsSource = _scannedGames;
            dgGames.Items.Refresh();
            UpdateGameCount();
            UpdateWiiListStatus();
        }

        private void BtnListSourceDisk2_Click(object sender, RoutedEventArgs e)
        {
            _currentListSource = GameListSource.Disk2;
            if (btnAddSource != null) btnAddSource.Visibility = Visibility.Collapsed;
            if (cboDiskDrive != null)
            {
                cboDiskDrive.Visibility = Visibility.Visible;
                LoadDrives();
                RestoreDiskDriveSelection(GameListSource.Disk2);
            }

            // 切换到磁盘2来源的列表
            _scannedGames = _disk2Games;
            dgGames.ItemsSource = _scannedGames;
            dgGames.Items.Refresh();
            UpdateGameCount();
            UpdateWiiListStatus();
        }

        private void RestoreDiskDriveSelection(GameListSource source)
        {
            if (cboDiskDrive == null || cboDiskDrive.Items == null) return;

            string root = source == GameListSource.Disk2 ? _disk2Root : _disk1Root;
            if (string.IsNullOrWhiteSpace(root)) return;

            foreach (var item in cboDiskDrive.Items)
            {
                var di = item as DriveItem;
                if (di == null || string.IsNullOrWhiteSpace(di.RootPath)) continue;

                if (string.Equals(di.RootPath, root, StringComparison.OrdinalIgnoreCase) ||
                    root.StartsWith(di.RootPath, StringComparison.OrdinalIgnoreCase))
                {
                    cboDiskDrive.SelectedItem = di;
                    break;
                }
            }
        }

        /// <summary>点击“选择”按钮时，打开悬浮菜单。</summary>
        private void BtnSelectMissingOnDisk_Click(object sender, RoutedEventArgs e)
        {
            if (btnSelectMissingOnDisk?.ContextMenu != null)
            {
                btnSelectMissingOnDisk.ContextMenu.PlacementTarget = btnSelectMissingOnDisk;
                btnSelectMissingOnDisk.ContextMenu.IsOpen = true;
            }
        }

        private void BtnSelectMenu_MouseEnter(object sender, MouseEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.ContextMenu == null) return;
            if (!btn.ContextMenu.IsOpen)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void MenuSelectMissingOnDisk1_Click(object sender, RoutedEventArgs e)
        {
            SelectMissingOnDiskFor(GameListSource.Disk1);
        }

        private void MenuSelectMissingOnDisk2_Click(object sender, RoutedEventArgs e)
        {
            SelectMissingOnDiskFor(GameListSource.Disk2);
        }

        /// <summary>根据指定磁盘（1 或 2），选择该磁盘上不存在的游戏。</summary>
        private void SelectMissingOnDiskFor(GameListSource source)
        {
            if (string.IsNullOrWhiteSpace(_disk1Root) && string.IsNullOrWhiteSpace(_disk2Root))
            {
                MessageBox.Show("请先在上方切换到“磁盘1”或“磁盘2”并选择一个本地磁盘。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string targetRoot = null;
            string diskName = null;
            if (source == GameListSource.Disk1)
            {
                targetRoot = _disk1Root;
                diskName = "磁盘1";
            }
            else if (source == GameListSource.Disk2)
            {
                targetRoot = _disk2Root;
                diskName = "磁盘2";
            }

            if (string.IsNullOrWhiteSpace(targetRoot) || !Directory.Exists(targetRoot))
            {
                MessageBox.Show($"{diskName} 路径未配置或不存在，请先在顶部选择对应磁盘。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var existingIds = ScanDiskGameIds(targetRoot);
            if (existingIds.Count == 0)
            {
                MessageBox.Show($"{diskName} 下未发现已存在的游戏（根据ID识别）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            foreach (var g in _scannedGames)
            {
                string id = g.GameId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    g.IsSelected = false;
                    continue;
                }
                g.IsSelected = !existingIds.Contains(id);
            }

            dgGames.Items.Refresh();
            UpdateWiiListStatus();
            MessageBox.Show($"已选择在 {diskName} 上不存在的游戏。", "选择完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>扫描指定根目录下已有的游戏ID（根据 wbfs/games 目录中的镜像文件与 GameID 解析）。</summary>
        private HashSet<string> ScanDiskGameIds(string rootPath)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string wiiDir = Path.Combine(rootPath, "wbfs");
                string gcDir = Path.Combine(rootPath, "games");
                var searchRoots = new List<string>();
                if (Directory.Exists(wiiDir)) searchRoots.Add(wiiDir);
                if (Directory.Exists(gcDir)) searchRoots.Add(gcDir);
                if (searchRoots.Count == 0 && Directory.Exists(rootPath))
                    searchRoots.Add(rootPath);

                foreach (var dir in searchRoots)
                {
                    foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".wbfs" && ext != ".iso" && ext != ".gcm" && ext != ".wad")
                            continue;

                        string id = null;

                        // 1) 优先从文件名中的 [ID] 或 ID6 提取
                        string name = Path.GetFileNameWithoutExtension(file);
                        int bracketIndex = name.LastIndexOf('[');
                        if (bracketIndex >= 0 && name.EndsWith("]"))
                        {
                            var inside = name.Substring(bracketIndex + 1, name.Length - bracketIndex - 2).Trim();
                            if (inside.Length >= 4)
                                id = inside;
                        }
                        if (string.IsNullOrWhiteSpace(id) && name.Length >= 4 && name.Length <= 8)
                        {
                            // 简单判断：文件名本身就是 ID
                            id = name;
                        }

                        // 2) 读取文件头信息（ISO/GCM/WBFS）
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            if (ext == ".iso" || ext == ".gcm")
                            {
                                if (DiscHeaderReader.TryReadDiscHeader(file, out var hid, out _, out _, out _))
                                    id = hid;
                            }
                            else if (ext == ".wbfs")
                            {
                                if (DiscHeaderReader.TryReadWbfsGameId(file, out var wid))
                                    id = wid;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(id))
                            ids.Add(id);
                    }
                }
            }
            catch
            {
                // 扫描失败时返回已收集到的（可能为空）
            }

            return ids;
        }

        /// <summary>从指定磁盘根路径扫描 wbfs / games 目录下的 Wii/NGC 游戏，填充当前游戏列表。</summary>
        private void ScanGamesFromDiskRoot(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                MessageBox.Show("选中的磁盘路径无效或已不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string wiiPath = Path.Combine(rootPath, "wbfs");
            string ngcPath = Path.Combine(rootPath, "games");

            // 根据当前来源选择对应列表
            List<GameInfo> targetList = _currentListSource == GameListSource.Disk2 ? _disk2Games : _disk1Games;
            targetList.Clear();

            bool any = false;
            if (Directory.Exists(wiiPath))
            {
                ScanAndAddGamesFromDirectory(wiiPath, clearExisting: false, targetList);
                any = true;
            }
            if (Directory.Exists(ngcPath))
            {
                ScanAndAddGamesFromDirectory(ngcPath, clearExisting: false, targetList);
                any = true;
            }

            if (!any)
            {
                MessageBox.Show("在所选磁盘根目录下未找到 'wbfs' 或 'games' 目录。\n请确认磁盘结构是否符合约定。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 如果当前列表指向此来源，同步刷新 UI
            if (_scannedGames == targetList)
            {
                dgGames.Items.Refresh();
                UpdateGameCount();
                UpdateWiiListStatus();
            }
        }

        /// <summary>
        /// 对选中的游戏进行重命名与整理：
        /// - 目标文件名：GameId.ext（例如：RMCP01.wbfs / RMCP01.iso）
        /// - 目标文件夹名：中文名 + 空格 + [GameId]（如果没有中文名，则使用英文名或 GameId）
        /// - 同一个游戏的分割 WBFS（.wbfs / .wbf1~.wbf4）会一起移动和重命名。
        /// </summary>
        private void BtnRenameGames_Click(object sender, RoutedEventArgs e)
        {
            // 提交 DataGrid 编辑，保证最新勾选状态
            try
            {
                dgGames.CommitEdit(DataGridEditingUnit.Cell, true);
                dgGames.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { /* ignore */ }

            var toRename = _scannedGames.Where(g => g.IsSelected).ToList();
            if (toRename.Count == 0)
                toRename = dgGames.SelectedItems.Cast<object>().OfType<GameInfo>().Distinct().ToList();

            if (toRename.Count == 0)
            {
                MessageBox.Show("没有勾选或选中任何游戏。请先勾选复选框或选中行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                    $"将对 {toRename.Count} 个游戏执行重命名与整理操作：\n\n" +
                    " - 文件名：GameID.扩展名（例如：RMCP01.wbfs）\n" +
                    " - 文件夹：中文名 + 空格 + [GameID]\n" +
                    " - 支持分割 WBFS（.wbfs/.wbf1/.wbf2/...）一起移动\n\n" +
                    "注意：此操作会在磁盘上移动/重命名实际文件！\n请确认相关游戏未被其他程序占用。",
                    "确认游戏重命名",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            int ok = 0;
            int fail = 0;
            var errors = new List<string>();

            foreach (var game in toRename)
            {
                if (string.IsNullOrWhiteSpace(game.Path) || !File.Exists(game.Path))
                {
                    fail++;
                    errors.Add($"{game.Title ?? game.GameId ?? "(未知游戏)"}: 文件不存在或路径为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(game.GameId))
                {
                    fail++;
                    errors.Add($"{game.Title ?? "(未知游戏)"}: 未识别到 GameID，无法按规则重命名。");
                    continue;
                }

                try
                {
                    string originalPath = game.Path;
                    string dir = Path.GetDirectoryName(originalPath);
                    string ext = Path.GetExtension(originalPath);

                    if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(ext))
                    {
                        fail++;
                        errors.Add($"{game.Title ?? game.GameId}: 无法解析原始路径或扩展名。");
                        continue;
                    }

                    // 文件夹名：中文名 [ID]，中文名为空时，用标题或 ID 替代
                    string displayName = !string.IsNullOrWhiteSpace(game.ChineseTitle)
                        ? game.ChineseTitle
                        : (!string.IsNullOrWhiteSpace(game.Title) ? game.Title : game.GameId);

                    // 替换非法字符
                    foreach (var c in Path.GetInvalidFileNameChars())
                    {
                        displayName = displayName.Replace(c, '_');
                    }

                    string folderName = $"{displayName} [{game.GameId}]";
                    string targetFolder = Path.Combine(dir, folderName);

                    if (!Directory.Exists(targetFolder))
                        Directory.CreateDirectory(targetFolder);

                    // 1) 分割 WBFS：需要处理 .wbfs/.wbf1~.wbf4
                    if (ext.Equals(".wbfs", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(originalPath);
                        var parts = new[] { ".wbfs", ".wbf1", ".wbf2", ".wbf3", ".wbf4" };

                        foreach (var partExt in parts)
                        {
                            string srcPart = Path.Combine(dir, baseName + partExt);
                            if (!File.Exists(srcPart)) continue;

                            // 新文件名：GameID.partExt（主文件是 GameID.wbfs，其他是 GameID.wbf1 等）
                            string destPartName = game.GameId + partExt;
                            string destPartPath = Path.Combine(targetFolder, destPartName);

                            if (!string.Equals(srcPart, destPartPath, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Move(srcPart, destPartPath);
                            }

                            // 主 wbfs 更新路径
                            if (partExt.Equals(".wbfs", StringComparison.OrdinalIgnoreCase))
                            {
                                game.Path = destPartPath;
                            }
                        }
                    }
                    else
                    {
                        // 2) 普通单文件：直接重命名为 GameID.ext 并移动到目标文件夹
                        string destFileName = game.GameId + ext.ToLowerInvariant();
                        string destPath = Path.Combine(targetFolder, destFileName);

                        if (!string.Equals(originalPath, destPath, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(originalPath, destPath);
                        }

                        game.Path = destPath;
                    }

                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    errors.Add($"{game.Title ?? game.GameId ?? "(未知游戏)"}: 重命名失败: {ex.Message}");
                }
            }

            dgGames.Items.Refresh();

            string summary = $"重命名完成：成功 {ok} 个，失败 {fail} 个。";
            if (fail > 0 && errors.Count > 0)
            {
                MessageBox.Show(
                    summary + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, errors.Take(5)) +
                    (errors.Count > 5 ? Environment.NewLine + "..." : string.Empty),
                    "游戏重命名（部分失败）",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(summary, "游戏重命名完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCopyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (btnCopyToDisk?.ContextMenu != null)
            {
                btnCopyToDisk.ContextMenu.PlacementTarget = btnCopyToDisk;
                btnCopyToDisk.ContextMenu.IsOpen = true;
            }
        }

        private void BtnCopyMenu_MouseEnter(object sender, MouseEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.ContextMenu == null) return;
            if (!btn.ContextMenu.IsOpen)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void MenuCopyToDisk1_Click(object sender, RoutedEventArgs e) => CopySelectedGamesToDisk(GameListSource.Disk1);
        private void MenuCopyToDisk2_Click(object sender, RoutedEventArgs e) => CopySelectedGamesToDisk(GameListSource.Disk2);

        private void MenuConvertToWbfs_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("转换为 WBFS 功能待实现（后续可接入 wit/wwt 或其它转换工具）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuConvertToIso_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("转换为 ISO 功能待实现（后续可接入 wit/wwt 或其它转换工具）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>将选中游戏拷贝到磁盘1或磁盘2（如果目标已存在该ID则跳过）。</summary>
        private void CopySelectedGamesToDisk(GameListSource targetDisk)
        {
            if (targetDisk != GameListSource.Disk1 && targetDisk != GameListSource.Disk2)
                return;

            string targetRoot = targetDisk == GameListSource.Disk1 ? _disk1Root : _disk2Root;
            string diskName = targetDisk == GameListSource.Disk1 ? "磁盘1" : "磁盘2";

            if (string.IsNullOrWhiteSpace(targetRoot) || !Directory.Exists(targetRoot))
            {
                MessageBox.Show($"请先切换到 {diskName} 并在下拉框选择盘符。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 选中要拷贝的游戏
            try
            {
                dgGames.CommitEdit(DataGridEditingUnit.Cell, true);
                dgGames.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { /* ignore */ }

            var toCopy = _scannedGames.Where(g => g.IsSelected).ToList();
            if (toCopy.Count == 0)
                toCopy = dgGames.SelectedItems.Cast<object>().OfType<GameInfo>().Distinct().ToList();

            if (toCopy.Count == 0)
            {
                MessageBox.Show("没有勾选或选中任何游戏。请先勾选复选框或选中行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 预先扫描目标磁盘已有的游戏ID，用于“存在则跳过”
            var existingIds = ScanDiskGameIds(targetRoot);

            int copied = 0;
            int skipped = 0;
            int failed = 0;
            var errors = new List<string>();

            string wiiDestRoot = Path.Combine(targetRoot, "wbfs");
            string ngcDestRoot = Path.Combine(targetRoot, "games");

            bool needWii = toCopy.Any(g => string.Equals(g.Platform, "Wii", StringComparison.OrdinalIgnoreCase));
            bool needNgc = toCopy.Any(IsNgcGame);

            if (needWii && !Directory.Exists(wiiDestRoot))
            {
                var ans = MessageBox.Show(
                    $"目标磁盘 {diskName} 下不存在 Wii 游戏目录 'wbfs'。\n是否创建该目录？",
                    "创建 wbfs 目录",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ans != MessageBoxResult.Yes) return;
                Directory.CreateDirectory(wiiDestRoot);
            }

            if (needNgc && !Directory.Exists(ngcDestRoot))
            {
                var ans = MessageBox.Show(
                    $"目标磁盘 {diskName} 下不存在 NGC 游戏目录 'games'。\n是否创建该目录？",
                    "创建 games 目录",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ans != MessageBoxResult.Yes) return;
                Directory.CreateDirectory(ngcDestRoot);
            }

            foreach (var game in toCopy)
            {
                string id = game.GameId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    failed++;
                    errors.Add($"{game.Title ?? "(未知游戏)"}: 未识别到 GameID，无法拷贝。");
                    continue;
                }

                if (existingIds.Contains(id))
                {
                    skipped++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(game.Path) || !File.Exists(game.Path))
                {
                    failed++;
                    errors.Add($"{game.Title ?? id}: 源文件不存在。");
                    continue;
                }

                try
                {
                    string srcPath = game.Path;
                    string ext = Path.GetExtension(srcPath);
                    string displayName = !string.IsNullOrWhiteSpace(game.ChineseTitle)
                        ? game.ChineseTitle
                        : (!string.IsNullOrWhiteSpace(game.Title) ? game.Title : id);

                    foreach (var c in Path.GetInvalidFileNameChars())
                        displayName = displayName.Replace(c, '_');

                    bool isNgc = IsNgcGame(game);
                    string baseRoot = isNgc ? ngcDestRoot : wiiDestRoot;
                    string folderName = $"{displayName} [{id}]";
                    string destFolder = Path.Combine(baseRoot, folderName);
                    if (!Directory.Exists(destFolder))
                        Directory.CreateDirectory(destFolder);

                    if (isNgc)
                    {
                        // NGC Dolphin 标准结构：game.iso (+ disc2.iso + sys/)
                        string srcDir = Path.GetDirectoryName(srcPath);
                        string extLower = ext.ToLowerInvariant();
                        if (string.IsNullOrEmpty(extLower))
                            extLower = ".iso";

                        // 主盘 game.iso
                        string srcGame = srcPath;
                        if (!string.Equals(Path.GetFileName(srcPath), "game" + extLower, StringComparison.OrdinalIgnoreCase))
                        {
                            // 如果扫描的是 disc2.iso 之类，尝试回到 game.iso
                            string altGame = Path.Combine(srcDir ?? string.Empty, "game" + extLower);
                            if (File.Exists(altGame))
                                srcGame = altGame;
                        }
                        string destGame = Path.Combine(destFolder, "game" + extLower);
                        File.Copy(srcGame, destGame, overwrite: false);

                        // 第二张盘 disc2.iso
                        string srcDisc2 = Path.Combine(srcDir ?? string.Empty, "disc2" + extLower);
                        if (File.Exists(srcDisc2))
                        {
                            string destDisc2 = Path.Combine(destFolder, "disc2" + extLower);
                            File.Copy(srcDisc2, destDisc2, overwrite: false);
                        }

                        // sys 目录
                        string srcSysDir = Path.Combine(srcDir ?? string.Empty, "sys");
                        if (Directory.Exists(srcSysDir))
                        {
                            string destSysDir = Path.Combine(destFolder, "sys");
                            CopyDirectoryRecursive(srcSysDir, destSysDir);
                        }
                    }
                    else if (ext.Equals(".wbfs", StringComparison.OrdinalIgnoreCase))
                    {
                        string srcDir = Path.GetDirectoryName(srcPath);
                        string baseName = Path.GetFileNameWithoutExtension(srcPath);
                        var parts = new[] { ".wbfs", ".wbf1", ".wbf2", ".wbf3", ".wbf4" };
                        foreach (var partExt in parts)
                        {
                            string partSrc = Path.Combine(srcDir, baseName + partExt);
                            if (!File.Exists(partSrc)) continue;

                            string destName = id + partExt;
                            string destPath = Path.Combine(destFolder, destName);
                            File.Copy(partSrc, destPath, overwrite: false);
                        }
                    }
                    else
                    {
                        string destName = id + ext.ToLowerInvariant();
                        string destPath = Path.Combine(destFolder, destName);
                        File.Copy(srcPath, destPath, overwrite: false);
                    }

                    existingIds.Add(id);
                    copied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{game.Title ?? id}: 拷贝失败: {ex.Message}");
                }
            }

            string summary = $"拷贝到 {diskName} 完成：成功 {copied} 个，跳过（已存在） {skipped} 个，失败 {failed} 个。";
            if (failed > 0 && errors.Count > 0)
            {
                MessageBox.Show(
                    summary + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, errors.Take(5)) +
                    (errors.Count > 5 ? Environment.NewLine + "..." : string.Empty),
                    "拷贝完成（部分失败）",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(summary, "拷贝完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveList_Click(object sender, RoutedEventArgs e)
        {
            var selected = _scannedGames.Where(g => g.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("没有勾选任何游戏。请先勾选要保存的项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
                var dlg = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                DefaultExt = "txt",
                FileName = "游戏列表.txt"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                File.WriteAllLines(dlg.FileName, selected.Select(g => g.GameId ?? ""), System.Text.Encoding.UTF8);
                MessageBox.Show($"已保存 {selected.Count} 个游戏ID到：{dlg.FileName}", "保存列表", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoadList_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                DefaultExt = "txt"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var lines = File.ReadAllLines(dlg.FileName, System.Text.Encoding.UTF8);
                var ids = new HashSet<string>(lines.Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)), StringComparer.OrdinalIgnoreCase);
                int count = 0;
                foreach (var g in _scannedGames)
                {
                    if (ids.Contains(g.GameId ?? ""))
                    {
                        g.IsSelected = true;
                        count++;
                    }
                }
                dgGames.Items.Refresh();
                UpdateWiiListStatus();
                MessageBox.Show($"已根据文件勾选 {count} 个游戏（文件中共 {ids.Count} 个ID）。", "加载列表", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            // 确保 DataGrid 内的勾选状态已提交（否则可能出现“刚勾选就点删除但未生效”）
            try
            {
                dgGames.CommitEdit(DataGridEditingUnit.Cell, true);
                dgGames.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { /* ignore */ }

            // 兼容两种“选中”：1) 勾选复选框 2) 选中行
            var toRemove = _scannedGames.Where(g => g.IsSelected).ToList();
            if (toRemove.Count == 0)
            {
                toRemove = dgGames.SelectedItems.Cast<object>().OfType<GameInfo>().Distinct().ToList();
            }

            if (toRemove.Count == 0)
            {
                MessageBox.Show("没有勾选或选中任何游戏。请先勾选复选框或选中行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                    $"确定要删除这 {toRemove.Count} 个游戏吗？\n\n" +
                    "此操作会删除对应的磁盘游戏文件，如果游戏所在文件夹变为空文件夹也会一并删除！\n\n" +
                    "请确认已经备份好重要数据。",
                    "确认删除（删除磁盘文件）",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            int okCount = 0;
            int failCount = 0;
            var errorList = new List<string>();

            foreach (var game in toRemove)
            {
                if (string.IsNullOrEmpty(game.Path))
                {
                    failCount++;
                    errorList.Add($"{game.Title ?? game.GameId ?? "(未知游戏)"}: 路径为空，无法删除。");
                    continue;
                }

                try
                {
                    string path = game.Path;
                    string dir = Path.GetDirectoryName(path);
                    string title = game.Title ?? game.GameId ?? Path.GetFileNameWithoutExtension(path);

                    // 1) 删除游戏文件（包含 .wbfs 分割文件）
                    if (path.EndsWith(".wbfs", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(path);
                        var parts = new[] { ".wbfs", ".wbf1", ".wbf2", ".wbf3", ".wbf4" };
                        foreach (var ext in parts)
                        {
                            string partPath = Path.Combine(dir, baseName + ext);
                            if (!File.Exists(partPath)) continue;
                            try
                            {
                                File.Delete(partPath);
                            }
                            catch (Exception exPart)
                            {
                                failCount++;
                                errorList.Add($"{title} ({ext}) 删除失败: {exPart.Message}");
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(path))
                        {
                            try
                            {
                                File.Delete(path);
                            }
                            catch (Exception exFile)
                            {
                                failCount++;
                                errorList.Add($"{title}: 删除文件失败: {exFile.Message}");
                            }
                        }
                    }

                    // 2) 如果所在目录已变成空文件夹，则尝试删除该目录
                    try
                    {
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir, false);
                            }
                        }
                    }
                    catch
                    {
                        // 删除空目录失败不算致命错误，忽略即可
                    }

                    okCount++;
                    _scannedGames.Remove(game);
                }
                catch (Exception ex)
                {
                    failCount++;
                    errorList.Add($"{game.Title ?? game.GameId ?? "(未知游戏)"}: 删除时出错: {ex.Message}");
                }
            }

            foreach (var g in toRemove)
                _scannedGames.Remove(g);

            dgGames.Items.Refresh();
            UpdateGameCount();
            UpdateWiiListStatus();
            if (failCount > 0 && errorList.Count > 0)
            {
                MessageBox.Show(
                    $"成功删除 {okCount} 个游戏，{failCount} 个删除失败。\n\n" +
                    string.Join(Environment.NewLine, errorList.Take(5)) +
                    (errorList.Count > 5 ? Environment.NewLine + "..." : string.Empty),
                    "删除完成（部分失败）",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show($"已成功删除 {okCount} 个游戏及其磁盘文件。", "删除完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void DgGames_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null && row.Item is GameInfo)
            {
                if (!dgGames.SelectedItems.Cast<GameInfo>().Contains(row.Item))
                {
                    dgGames.SelectedItems.Clear();
                    dgGames.SelectedItems.Add(row.Item);
                }
            }
        }

        private void DgGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgGames.SelectedItem is GameInfo game)
            {
                UpdateGameVisuals(game);
                UpdateGameExtraInfo(game);
            }
            else
            {
                UpdateGameVisuals(null);
                UpdateGameExtraInfo(null);
            }
        }

        private void MenuSearchGameVideo_Click(object sender, RoutedEventArgs e)
        {
            var game = dgGames.SelectedItem as GameInfo ?? _scannedGames.FirstOrDefault(g => g.IsSelected);
            if (game == null)
            {
                MessageBox.Show("请先选中或勾选一个游戏。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string query = $"{game.Title} wii play";
            string url = "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(query);
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("无法打开浏览器：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void MenuOpenGameLocation_Click(object sender, RoutedEventArgs e)
        {
            var game = dgGames.SelectedItem as GameInfo ?? _scannedGames.FirstOrDefault(g => g.IsSelected);
            if (game == null || string.IsNullOrEmpty(game.Path))
            {
                MessageBox.Show("请先选中或勾选一个游戏，且该游戏有路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string folder = Path.GetDirectoryName(game.Path);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("游戏所在目录不存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("无法打开文件夹：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        #endregion

        #region 封面与游戏详情相关方法

        private void LoadCoverPath()
        {
            // 从设置中加载封面路径
            var settings = SettingsManager.GetSettings();
            _coverPath = settings.CoverPath;
            txtCoverPath.Text = _coverPath;
        }

        /// <summary>更新右侧封面预览（Disc + 3D）。</summary>
        private void UpdateGameVisuals(GameInfo game)
        {
            if (imgDiscCover == null || img3DCover == null)
                return;

            if (game == null || string.IsNullOrEmpty(_coverPath) || string.IsNullOrEmpty(game.GameId))
            {
                imgDiscCover.Source = null;
                img3DCover.Source = null;
                return;
            }

            string id = game.GameId;
            // Disc 封面
            string discPath = TryResolveCoverPath(id, "disc");
            // 3D 封面
            string cover3dPath = TryResolveCoverPath(id, "3d");

            imgDiscCover.Source = LoadBitmapOrNull(discPath);
            img3DCover.Source = LoadBitmapOrNull(cover3dPath);
        }

        /// <summary>更新下方“游戏简介 + 游戏信息”区域。</summary>
        private void UpdateGameExtraInfo(GameInfo game)
        {
            if (txtSynopsis == null || txtGameInfo == null)
                return;

            if (game == null)
            {
                txtSynopsis.Text = string.Empty;
                txtGameInfo.Text = string.Empty;
                return;
            }

            // 简介：优先显示 wiitdb 中的 synopsis，没有则提示“暂无简介”
            if (!string.IsNullOrWhiteSpace(game.Synopsis))
                txtSynopsis.Text = game.Synopsis.Trim();
            else
                txtSynopsis.Text = "暂无简介。";

            // 组装游戏信息（类似 Wii 游戏查询里的详情 + synopsis）
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"游戏ID: {game.GameId}");
            if (!string.IsNullOrWhiteSpace(game.Title))
                sb.AppendLine($"游戏名称: {game.Title}");
            if (!string.IsNullOrWhiteSpace(game.ChineseTitle))
                sb.AppendLine($"中文名称: {game.ChineseTitle}");
            if (!string.IsNullOrWhiteSpace(game.Platform))
                sb.AppendLine($"平台: {game.Platform}");
            if (!string.IsNullOrWhiteSpace(game.PlatformType))
                sb.AppendLine($"平台类型: {game.PlatformType}");
            if (!string.IsNullOrWhiteSpace(game.Region))
                sb.AppendLine($"区域: {game.Region}");
            if (game.Players > 0)
                sb.AppendLine($"玩家数量: {game.Players}");
            if (!string.IsNullOrWhiteSpace(game.Publisher))
                sb.AppendLine($"发行商: {game.Publisher}");
            if (!string.IsNullOrWhiteSpace(game.Developer))
                sb.AppendLine($"开发商: {game.Developer}");
            if (game.Controllers != null && game.Controllers.Any())
                sb.AppendLine("控制器: " + string.Join(", ", game.Controllers));
            if (game.Genres != null && game.Genres.Any())
                sb.AppendLine("游戏类型: " + string.Join(", ", game.Genres));
            if (game.Languages != null && game.Languages.Any())
                sb.AppendLine("支持语言: " + string.Join(", ", game.Languages));

            txtGameInfo.Text = sb.ToString();
        }

        private static BitmapImage LoadBitmapOrNull(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private string TryResolveCoverPath(string gameId, string typeFolder)
        {
            if (string.IsNullOrEmpty(_coverPath) || string.IsNullOrEmpty(gameId))
                return null;

            string fileName = gameId + ".png";
            // 优先小写目录
            string path = System.IO.Path.Combine(_coverPath, typeFolder.ToLowerInvariant(), fileName);
            if (File.Exists(path)) return path;

            // 再尝试首字母大写
            string cap = char.ToUpperInvariant(typeFolder[0]) + typeFolder.Substring(1).ToLowerInvariant();
            path = System.IO.Path.Combine(_coverPath, cap, fileName);
            if (File.Exists(path)) return path;

            return null;
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(destDir, fileName);
                try
                {
                    File.Copy(file, destPath, overwrite: false);
                }
                catch
                {
                    // 忽略单个文件复制失败
                }
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                try
                {
                    CopyDirectoryRecursive(dir, destSubDir);
                }
                catch
                {
                    // 忽略子目录复制失败
                }
            }
        }

        #endregion

        #region 浏览按钮事件处理

        private void BtnBrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            using (var folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowser.Description = "选择游戏存储路径";
                if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtGamePath.Text = folderBrowser.SelectedPath;
                }
            }
        }

        private void BtnBrowseDatabasePath_Click(object sender, RoutedEventArgs e)
        {
            using (var folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowser.Description = "选择数据库路径";
                if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtDatabasePath.Text = folderBrowser.SelectedPath;
                }
            }
        }

        private void BtnBrowseCoverPath_Click(object sender, RoutedEventArgs e)
        {
            using (var folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowser.Description = "选择封面存储路径";
                if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtCoverPath.Text = folderBrowser.SelectedPath;
                    _coverPath = folderBrowser.SelectedPath;
                }
            }
        }

        #endregion

        #region 设置相关方法

        private void LoadSettings()
        {
            try
            {
                var settings = SettingsManager.GetSettings();
                
                // 加载通用设置
                chkAutoUpdate.IsChecked = settings.AutoUpdate;
                chkEnableLogging.IsChecked = settings.EnableLogging;
                chkCheckDevices.IsChecked = settings.CheckDevices;
                
                // 加载路径设置
                txtGamePath.Text = settings.GamePath;
                txtDatabasePath.Text = settings.DatabasePath;
                txtCoverPath.Text = settings.CoverPath;
                _coverPath = settings.CoverPath;

                // 加载网络设置
                txtApiKey.Text = settings.ApiKey;
                chkEnableProxy.IsChecked = settings.EnableProxy;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new AppSettings
                {
                    // 保存通用设置
                    AutoUpdate = chkAutoUpdate.IsChecked ?? false,
                    EnableLogging = chkEnableLogging.IsChecked ?? false,
                    CheckDevices = chkCheckDevices.IsChecked ?? false,

                    // 保存路径设置
                    GamePath = txtGamePath.Text,
                    DatabasePath = txtDatabasePath.Text,
                    CoverPath = txtCoverPath.Text,
                    // LastScanPath 目前不再由界面直接编辑，这里保留为之前的值，避免丢失历史信息
                    LastScanPath = SettingsManager.GetSettings().LastScanPath,

                    // 保存网络设置
                    ApiKey = txtApiKey.Text,
                    EnableProxy = chkEnableProxy.IsChecked ?? false
                };
                
                SettingsManager.UpdateSettings(settings);
                
                // 更新当前的封面路径
                _coverPath = settings.CoverPath;
                
                MessageBox.Show("设置保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 游戏查询窗口

        private void BtnGameQuery_Click(object sender, RoutedEventArgs e)
        {
            // 打开Wii游戏查询窗口，并传递当前的封面路径
            var queryWindow = new WiiGameQueryWindow(_coverPath)
            {
                Owner = this
            };
            queryWindow.ShowDialog();
        }

        #endregion
    }
}
