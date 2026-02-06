using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private List<GameInfo> _scannedGames;  // 扫描得到的 Wii/NGC 游戏列表
        private string _coverPath;
        private readonly WiiGameIdentifier _wiiIdentifier = new WiiGameIdentifier();
        private readonly NgcGameIdentifier _ngcIdentifier = new NgcGameIdentifier();

        public MainWindow()
        {
            InitializeComponent();
            _scannedGames = new List<GameInfo>();
            dgGames.ItemsSource = _scannedGames;
            InitializeDatabase();
            SetupEventHandlers();
            LoadSettings();
            LoadCoverPath();
            LoadDrives();
            UpdateGameCount(); // 初始化时显示 0
            UpdateWiiListStatus();
            cboTargetDrive.SelectionChanged += (s, _) => UpdateWiiListStatus();
            var statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            statusTimer.Tick += (s, _) => UpdateWiiListStatus();
            statusTimer.Start();
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
                txtCopyStatus.Text = "无法枚举驱动器: " + ex.Message;
            }
            cboTargetDrive.ItemsSource = drives;
            if (cboTargetDrive.Items.Count > 0)
                cboTargetDrive.SelectedIndex = 0;
        }

        private void BtnRefreshDrives_Click(object sender, RoutedEventArgs e)
        {
            LoadDrives();
            txtCopyStatus.Text = "已刷新驱动器列表。";
        }

        /// <summary>更新游戏数量统计显示</summary>
        private void UpdateGameCount()
        {
            int wiiCount = _scannedGames.Count(g => g.Platform == "Wii");
            int ngcCount = _scannedGames.Count(g => g.Platform == "NGC");
            txtGameCount.Text = $"Wii: {wiiCount}  |  NGC: {ngcCount}";
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
            if (cboTargetDrive?.SelectedItem is DriveItem di && !string.IsNullOrEmpty(di.RootPath))
            {
                try
                {
                    string root = di.RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (root.Length > 0) root += Path.DirectorySeparatorChar;
                    var d = new DriveInfo(root);
                    if (d.IsReady)
                        driveInfo = $"  目标分区: 剩余 {FormatSize(d.TotalFreeSpace)} | 总 {FormatSize(d.TotalSize)}";
                }
                catch { driveInfo = "  目标分区: —"; }
            }
            else
                driveInfo = "  目标分区: —";
            txtWiiListStatus.Text = $"选中: {selCount} 个，共 {FormatSize(selSize)}{driveInfo}";
        }

        #region Wii 扫描与拷贝

        private void BtnSelectScanPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择包含 Wii/NGC 游戏的目录";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtScanPath.Text = dlg.SelectedPath;
                }
            }
        }

        private void BtnScanGames_Click(object sender, RoutedEventArgs e)
        {
            string path = txtScanPath.Text?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("请先选择要扫描的游戏目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!Directory.Exists(path))
            {
                MessageBox.Show("所选目录不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _scannedGames.Clear();
            UpdateGameCount(); // 清空时重置统计
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
                    var game = _wiiIdentifier.IdentifyGame(wbfsPath);
                    if (game == null) continue;
                    game.Size = totalSize;
                    game.GameId = baseName;
                    EnrichGameFromDatabase(game, baseName);
                    _scannedGames.Add(game);
                }

                // 2) 单文件游戏：仅 .iso / .wad / .gcm（.wbfs 已在上面分组中处理，含单独一个 .wbfs 的情况）
                foreach (string file in allFiles)
                {
                    string ext = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(ext)) continue;
                    if (!ext.Equals(".iso", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".wad", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".gcm", StringComparison.OrdinalIgnoreCase))
                        continue;

                    GameInfo game = null;
                    if (_wiiIdentifier.IsSupportedFormat(file))
                        game = _wiiIdentifier.IdentifyGame(file);
                    else if (_ngcIdentifier.IsSupportedFormat(file))
                        game = _ngcIdentifier.IdentifyGame(file);

                    if (game != null)
                    {
                        string baseName = Path.GetFileNameWithoutExtension(file);
                        game.GameId = baseName;
                        EnrichGameFromDatabase(game, baseName);
                        _scannedGames.Add(game);
                    }
                }

                dgGames.Items.Refresh();
                UpdateGameCount();
                // 自动保存本次扫描的目录，下次启动时恢复
                try
                {
                    var settings = SettingsManager.GetSettings();
                    settings.LastScanPath = path;
                    SettingsManager.UpdateSettings(settings);
                }
                catch { /* 忽略保存失败 */ }
                MessageBox.Show($"扫描完成，共找到 {_scannedGames.Count} 个游戏。", "扫描完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                game.Title = dbGame.Title ?? baseName;
                game.ChineseTitle = dbGame.ChineseTitle ?? string.Empty;
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
            var toRemove = _scannedGames.Where(g => g.IsSelected).ToList();
            if (toRemove.Count == 0)
            {
                MessageBox.Show("没有勾选任何游戏。请先勾选要删除的项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"确定要从列表中删除选中的 {toRemove.Count} 个游戏吗？\n（不会删除磁盘上的文件）", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            foreach (var g in toRemove)
                _scannedGames.Remove(g);
            dgGames.Items.Refresh();
            UpdateGameCount();
            UpdateWiiListStatus();
            MessageBox.Show($"已从列表移除 {toRemove.Count} 个游戏。", "删除完成", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void BtnCopyToDevice_Click(object sender, RoutedEventArgs e)
        {
            var toCopy = _scannedGames.Where(g => g.IsSelected).ToList();
            if (toCopy.Count == 0)
                toCopy = dgGames.SelectedItems.Cast<GameInfo>().ToList();
            if (toCopy.Count == 0)
                toCopy = _scannedGames;
            if (toCopy.Count == 0)
            {
                MessageBox.Show("没有可拷贝的游戏。请先扫描目录或选中要拷贝的游戏。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var driveItem = cboTargetDrive.SelectedItem as DriveItem;
            if (driveItem == null || string.IsNullOrEmpty(driveItem.RootPath))
            {
                MessageBox.Show("请选择目标分区。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string destRoot = driveItem.RootPath;
            if (!Directory.Exists(destRoot))
            {
                MessageBox.Show("目标分区不可用或不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Wii 常用目录名；NGC 可放 games 或根目录
            string destFolder = Path.Combine(destRoot, "wbfs");
            try
            {
                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法在目标分区创建目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int copied = 0;
            int failed = 0;
            var errors = new List<string>();
            foreach (var game in toCopy)
            {
                if (string.IsNullOrEmpty(game.Path))
                {
                    failed++;
                    errors.Add($"{game.Title}: 路径为空");
                    continue;
                }
                txtCopyStatus.Text = $"正在复制: {game.Title}...";
                bool gameOk = true;
                if (game.Path.EndsWith(".wbfs", StringComparison.OrdinalIgnoreCase))
                {
                    string dir = Path.GetDirectoryName(game.Path);
                    string baseName = Path.GetFileNameWithoutExtension(game.Path);
                    var parts = new[] { ".wbfs", ".wbf1", ".wbf2", ".wbf3", ".wbf4" };
                    foreach (var ext in parts)
                    {
                        string partPath = Path.Combine(dir, baseName + ext);
                        if (!File.Exists(partPath)) continue;
                        string destPath = Path.Combine(destFolder, baseName + ext);
                        try { File.Copy(partPath, destPath, overwrite: true); }
                        catch (Exception ex) { gameOk = false; errors.Add($"{game.Title} ({ext}): {ex.Message}"); break; }
                    }
                    if (gameOk) copied++;
                    else failed++;
                }
                else
                {
                    if (!File.Exists(game.Path))
                    {
                        failed++;
                        errors.Add($"{game.Title}: 文件不存在");
                        continue;
                    }
                    string fileName = Path.GetFileName(game.Path);
                    string destPath = Path.Combine(destFolder, fileName);
                    try
                    {
                        File.Copy(game.Path, destPath, overwrite: true);
                        copied++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"{game.Title}: {ex.Message}");
                    }
                }
            }

            txtCopyStatus.Text = $"复制完成: 成功 {copied} 个，失败 {failed} 个。";
            if (failed > 0 && errors.Count > 0)
                MessageBox.Show(string.Join(Environment.NewLine, errors.Take(5)) + (errors.Count > 5 ? Environment.NewLine + "..." : ""), "部分复制失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            else if (copied > 0)
                MessageBox.Show($"已成功复制 {copied} 个游戏到 {destFolder}", "复制完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 封面相关方法

        private void LoadCoverPath()
        {
            // 从设置中加载封面路径
            var settings = SettingsManager.GetSettings();
            _coverPath = settings.CoverPath;
            txtCoverPath.Text = _coverPath;
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
                txtScanPath.Text = settings.LastScanPath ?? string.Empty;

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
                    LastScanPath = txtScanPath.Text ?? string.Empty,

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
