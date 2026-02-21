using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using U_Wii_X_Fusion.Core.Settings;

namespace U_Wii_X_Fusion
{
    public partial class WiiPluginEditorWindow : Window
    {
        private string _pluginsDir = "";
        private string _romsDir = "";
        private string _imagesDir = "";
        private string _customTitlesFile = "";
        private readonly ObservableCollection<PluginItem> _plugins = new ObservableCollection<PluginItem>();
        private PluginItem _currentPlugin;
        private Dictionary<string, Dictionary<string, string>> _customTitles = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private bool _customTitlesModified;
        private bool _languageZh = true;
        private bool _topmost;
        private string _logPath;
        private readonly ObservableCollection<RomRow> _romRows = new ObservableCollection<RomRow>();
        private readonly ObservableCollection<ImageRow> _imageRows = new ObservableCollection<ImageRow>();

        public WiiPluginEditorWindow()
        {
            InitializeComponent();
            _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wii_plugin_tool.log");
            chkLog.Checked += (s, _) => Log("日志已启用");
            chkLog.Unchecked += (s, _) => Log("日志已禁用");
            if (App.GetWindowIcon() != null) Icon = App.GetWindowIcon();
            listPlugins.ItemsSource = _plugins;
            dgRoms.ItemsSource = _romRows;
            dgImages.ItemsSource = _imageRows;
            dgRoms.LoadingRow += DgRoms_LoadingRow;
            Closing += (s, e) =>
            {
                if (_customTitlesModified && MessageBox.Show("自定义标题已修改，是否保存？", "保存", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    SaveCustomTitles();
            };
            LoadLastPaths();
            _languageZh = !AppLanguage.IsEnglish;
            ApplyLanguage();
        }

        /// <summary>由主窗口在切换全局语言时调用，使插件编辑器与主界面语言一致</summary>
        public void ApplyLanguageFromGlobal()
        {
            _languageZh = !AppLanguage.IsEnglish;
            ApplyLanguage();
        }

        private string L(string zh, string en) => _languageZh ? zh : en;

        private void ApplyLanguage()
        {
            // 窗口标题
            Title = L("WII 插件编辑器", "WII Plugin Editor");

            // 工具栏按钮与提示
            btnPin.Content = L("📌 置顶", "📌 Pin");
            btnPin.ToolTip = L("切换窗口置顶", "Toggle window always on top");

            btnPluginsDir.Content = L("插件目录", "Plugins dir");
            btnPluginsDir.ToolTip = L("选择插件配置目录", "Select plugin configuration directory");

            btnImagesDir.Content = L("图片目录", "Images dir");
            btnImagesDir.ToolTip = L("选择图片目录", "Select images directory");

            btnTitlesFile.Content = L("标题文件", "Title file");
            btnTitlesFile.ToolTip = L("选择自定义标题文件", "Select custom titles file");

            btnRomsDir.Content = L("ROM目录", "ROM dir");
            btnRomsDir.ToolTip = L("选择ROM目录", "Select ROM directory");

            btnScanPlugins.Content = L("🔄 扫描插件", "🔄 Scan plugins");
            btnScanPlugins.ToolTip = L("扫描并加载插件", "Scan and load plugin ini files");

            chkSubdirs.Content = L("扫描子目录", "Scan subfolders");
            btnOpenTitles.Content = L("打开自定义标题", "Open custom titles");
            btnOpenTitles.ToolTip = L("用系统默认程序打开标题文件", "Open custom titles file with system editor");

            btnMameToTitles.Content = L("MAME列表→标题", "MAME list → titles");
            btnMameToTitles.ToolTip = L("从 mame_cn.lst 生成标题", "Generate titles from mame_cn.lst");

            btnLang.Content = L("🌐 语言", "🌐 Language");
            chkLog.Content = L("日志", "Log");

            // 分栏标题
            lblPluginList.Text = L("插件列表", "Plugin list");
            lblRomList.Text = L("ROM列表", "ROM list");
            lblImageList.Text = L("图片列表", "Image list");

            // 表头
            colRomFileName.Header = L("文件名", "File Name");
            colRomDisplayName.Header = L("显示名称", "Display Name");
            colImageFileName.Header = L("文件名", "File Name");
            colImageResolution.Header = L("分辨率", "Resolution");

            // 路径标签
            txtPluginsPath.Text = string.IsNullOrEmpty(_pluginsDir)
                ? L("插件目录: 未选择", "Plugins dir: Not selected")
                : L("插件目录: " + _pluginsDir, "Plugins dir: " + _pluginsDir);
            txtRomsPath.Text = string.IsNullOrEmpty(_romsDir)
                ? L("ROM目录: 未选择", "ROM dir: Not selected")
                : L("ROM目录: " + _romsDir, "ROM dir: " + _romsDir);
            txtImagesPath.Text = string.IsNullOrEmpty(_imagesDir)
                ? L("图片目录: 未选择", "Images dir: Not selected")
                : L("图片目录: " + _imagesDir, "Images dir: " + _imagesDir);
            txtTitlesPath.Text = string.IsNullOrEmpty(_customTitlesFile)
                ? L("自定义标题文件: 未选择", "Titles file: Not selected")
                : L("自定义标题文件: " + _customTitlesFile, "Titles file: " + _customTitlesFile);

            // 缺封面统计文案
            int missing = _romRows.Count(r => !r.HasImage);
            txtMissingCover.Text = missing > 0 ? L("  缺封面: " + missing, "  Missing covers: " + missing) : "";
        }

        private void LoadLastPaths()
        {
            var s = SettingsManager.GetSettings();
            if (!string.IsNullOrEmpty(s.LastPluginEditorPluginsDir) && Directory.Exists(s.LastPluginEditorPluginsDir))
            { _pluginsDir = s.LastPluginEditorPluginsDir; }
            if (!string.IsNullOrEmpty(s.LastPluginEditorRomsDir) && Directory.Exists(s.LastPluginEditorRomsDir))
            { _romsDir = s.LastPluginEditorRomsDir; }
            if (!string.IsNullOrEmpty(s.LastPluginEditorImagesDir) && Directory.Exists(s.LastPluginEditorImagesDir))
            { _imagesDir = s.LastPluginEditorImagesDir; }
            if (!string.IsNullOrEmpty(s.LastPluginEditorTitlesFile) && File.Exists(s.LastPluginEditorTitlesFile))
            { _customTitlesFile = s.LastPluginEditorTitlesFile; }
        }

        private void SaveLastPaths()
        {
            var s = SettingsManager.GetSettings();
            s.LastPluginEditorPluginsDir = _pluginsDir ?? string.Empty;
            s.LastPluginEditorRomsDir = _romsDir ?? string.Empty;
            s.LastPluginEditorImagesDir = _imagesDir ?? string.Empty;
            s.LastPluginEditorTitlesFile = _customTitlesFile ?? string.Empty;
            SettingsManager.UpdateSettings(s);
        }

        private void DgRoms_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is RomRow row)
            {
                e.Row.Background = row.HasImage ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 200));
            }
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            _topmost = !_topmost;
            Topmost = _topmost;
        }

        private void BtnPluginsDir_Click(object sender, RoutedEventArgs e)
        {
            var dir = VistaFolderPicker.PickFolder("选择插件目录", _pluginsDir, this);
            if (dir != null)
            {
                _pluginsDir = dir;
                Log("设置插件目录: " + dir);
                SaveLastPaths();
                ApplyLanguage();
            }
        }

        private void BtnImagesDir_Click(object sender, RoutedEventArgs e)
        {
            var dir = VistaFolderPicker.PickFolder("选择图片目录", _imagesDir, this);
            if (dir != null)
            {
                _imagesDir = dir;
                SaveLastPaths();
                ApplyLanguage();
            }
        }

        private void BtnTitlesFile_Click(object sender, RoutedEventArgs e)
        {
            var initialDir = !string.IsNullOrEmpty(_customTitlesFile) && File.Exists(_customTitlesFile)
                ? Path.GetDirectoryName(_customTitlesFile)
                : (!string.IsNullOrEmpty(_pluginsDir) && Directory.Exists(_pluginsDir) ? _pluginsDir : null);
            var dlg = new OpenFileDialog { Filter = "INI 文件 (*.ini)|*.ini|所有文件|*.*" };
            if (!string.IsNullOrEmpty(initialDir)) dlg.InitialDirectory = initialDir;
            if (dlg.ShowDialog() == true)
            {
                _customTitlesFile = dlg.FileName;
                SaveLastPaths();
                ApplyLanguage();
            }
        }

        private void BtnRomsDir_Click(object sender, RoutedEventArgs e)
        {
            var dir = VistaFolderPicker.PickFolder("选择ROM目录", _romsDir, this);
            if (dir != null)
            {
                _romsDir = dir;
                SaveLastPaths();
                ApplyLanguage();
            }
        }

        private void BtnScanPlugins_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pluginsDir)) return;
            _plugins.Clear();
            var searchOption = chkSubdirs.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(_pluginsDir, "*.ini", searchOption);
            var list = new List<PluginItem>();
            foreach (var f in files)
            {
                var plugin = ParsePlugin(f);
                if (plugin != null) list.Add(plugin);
            }
            foreach (var p in list.OrderBy(x => x.DisplayName))
                _plugins.Add(p);
            Log("扫描插件完成: " + _plugins.Count + " 个");
        }

        private static PluginItem ParsePlugin(string filePath)
        {
            try
            {
                var sections = IniHelper.ReadAllSections(filePath);
                if (!sections.TryGetValue("PLUGIN", out var section)) return null;
                var displayName = section.TryGetValue("displayname", out var dn) ? dn : Path.GetFileNameWithoutExtension(filePath);
                var fileTypes = section.TryGetValue("filetypes", out var ft) ? ft : "";
                var romDir = section.TryGetValue("romdir", out var rd) ? rd : "";
                var coverFolder = section.TryGetValue("coverfolder", out var cf) ? cf : "";
                var magic = section.TryGetValue("magic", out var mg) ? mg : "";
                return new PluginItem
                {
                    DisplayName = displayName,
                    FilePath = filePath,
                    FileTypes = fileTypes,
                    RomDir = romDir,
                    CoverFolder = coverFolder,
                    Magic = magic
                };
            }
            catch { return null; }
        }

        private void ListPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listPlugins.SelectedItem is PluginItem p)
            {
                _currentPlugin = p;
                LoadCustomTitles();
                LoadRoms();
                LoadImages();
            }
        }

        private void LoadCustomTitles()
        {
            var path = !string.IsNullOrEmpty(_customTitlesFile) ? _customTitlesFile : (string.IsNullOrEmpty(_pluginsDir) ? null : Path.Combine(Path.GetDirectoryName(_pluginsDir), "custom_titles.ini"));
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            _customTitles = IniHelper.ReadAllSections(path);
        }

        private string GetDisplayName(string fileName)
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var magic = _currentPlugin?.Magic ?? "";
            if (!string.IsNullOrEmpty(magic) && _customTitles.TryGetValue(magic, out var section) && section.TryGetValue(baseName, out var title))
                return title.Trim();
            return baseName;
        }

        private void LoadRoms()
        {
            _romRows.Clear();
            if (_currentPlugin == null || string.IsNullOrEmpty(_romsDir)) { txtRomCount.Text = "(0)"; txtMissingCover.Text = ""; return; }
            var romDir = Path.Combine(_romsDir, _currentPlugin.RomDir ?? "");
            romDir = Path.GetFullPath(romDir);
            if (!Directory.Exists(romDir))
            {
                MessageBox.Show("ROM目录不存在:\n" + romDir, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtRomCount.Text = "(0)"; txtMissingCover.Text = ""; return;
            }
            var fileTypes = ( _currentPlugin.FileTypes ?? "" ).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToList();
            if (fileTypes.Count == 0)
            {
                MessageBox.Show("插件未配置有效的 filetypes。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtRomCount.Text = "(0)"; txtMissingCover.Text = ""; return;
            }
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            imageDir = Path.GetFullPath(imageDir);
            var romFiles = new List<(string fileName, string displayName)>();
            foreach (var entry in Directory.EnumerateFileSystemEntries(romDir))
            {
                if (File.Exists(entry))
                {
                    var name = Path.GetFileName(entry);
                    var ext = (Path.GetExtension(name) ?? "").ToLowerInvariant();
                    if (fileTypes.Any(t => ext == t || (t.StartsWith(".") ? ext == t : "." + t == ext)))
                    {
                        romFiles.Add((name, GetDisplayName(name)));
                    }
                }
                else if (Directory.Exists(entry))
                {
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(entry))
                        {
                            var name = Path.GetFileName(f);
                            var ext = (Path.GetExtension(name) ?? "").ToLowerInvariant();
                            if (fileTypes.Any(t => ext == t || (t.StartsWith(".") ? ext == t : "." + t == ext)))
                                romFiles.Add((name, GetDisplayName(name)));
                        }
                    }
                    catch { }
                }
            }
            foreach (var (fileName, displayName) in romFiles.OrderBy(x => x.displayName))
            {
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName) ?? "";
                var imageName = baseName + ext + ".png";
                var imagePath = Path.Combine(imageDir, imageName);
                var hasImage = File.Exists(imagePath);
                _romRows.Add(new RomRow { FileName = fileName, DisplayName = displayName, HasImage = hasImage });
            }
            txtRomCount.Text = "(" + _romRows.Count + ")";
            int missing = _romRows.Count(r => !r.HasImage);
            txtMissingCover.Text = missing > 0 ? L("  缺封面: " + missing, "  Missing covers: " + missing) : "";
        }

        private void LoadImages()
        {
            _imageRows.Clear();
            imgPreview.Source = null;
            if (_currentPlugin == null || string.IsNullOrEmpty(_imagesDir)) { txtImageCount.Text = "(0)"; return; }
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            imageDir = Path.GetFullPath(imageDir);
            if (!Directory.Exists(imageDir)) { txtImageCount.Text = "(0)"; return; }
            var found = new List<(string fileName, string resolution)>();
            foreach (var row in _romRows)
            {
                var baseName = Path.GetFileNameWithoutExtension(row.FileName);
                var ext = Path.GetExtension(row.FileName) ?? "";
                var imageName = baseName + ext + ".png";
                var imagePath = Path.Combine(imageDir, imageName);
                if (!File.Exists(imagePath)) continue;
                string res = "未知";
                try
                {
                    using (var img = System.Drawing.Image.FromFile(imagePath))
                        res = img.Width + "×" + img.Height;
                }
                catch { }
                found.Add((imageName, res));
            }
            foreach (var (fileName, resolution) in found.OrderBy(x => x.fileName))
                _imageRows.Add(new ImageRow { FileName = fileName, Resolution = resolution });
            txtImageCount.Text = "(" + _imageRows.Count + ")";
        }

        private void DgRoms_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_currentPlugin == null || e.EditAction != DataGridEditAction.Commit) return;
            if (e.Column?.Header?.ToString() != "显示名称") return;
            var row = e.Row.Item as RomRow;
            if (row == null) return;
            var box = e.EditingElement as TextBox;
            var newName = box?.Text?.Trim();
            if (string.IsNullOrEmpty(newName)) return;
            var baseName = Path.GetFileNameWithoutExtension(row.FileName);
            var magic = _currentPlugin.Magic ?? "";
            if (string.IsNullOrEmpty(magic)) return;
            if (!_customTitles.ContainsKey(magic)) _customTitles[magic] = new Dictionary<string, string>(StringComparer.Ordinal);
            _customTitles[magic][baseName] = newName;
            _customTitlesModified = true;
        }

        private void SaveCustomTitles()
        {
            if (_currentPlugin == null || string.IsNullOrEmpty(_currentPlugin.Magic)) return;
            var path = !string.IsNullOrEmpty(_customTitlesFile) ? _customTitlesFile : Path.Combine(Path.GetDirectoryName(_pluginsDir), "custom_titles.ini");
            if (string.IsNullOrEmpty(path)) return;
            IniHelper.WriteSection(path, _currentPlugin.Magic, _customTitles.TryGetValue(_currentPlugin.Magic, out var sec) ? sec : new Dictionary<string, string>(StringComparer.Ordinal));
            _customTitlesModified = false;
        }

        private void BtnOpenTitles_Click(object sender, RoutedEventArgs e)
        {
            var path = !string.IsNullOrEmpty(_customTitlesFile) ? _customTitlesFile : (string.IsNullOrEmpty(_pluginsDir) ? null : Path.Combine(Path.GetDirectoryName(_pluginsDir), "custom_titles.ini"));
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("请先选择自定义标题文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!File.Exists(path))
            {
                try
                {
                    File.WriteAllText(path, "; 自定义标题\n; [magic]\n; ROM基名=显示名称\n", new UTF8Encoding(false));
                }
                catch (Exception ex) { MessageBox.Show("创建文件失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            }
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("打开失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnMameToTitles_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlugin == null) { MessageBox.Show("请先选择插件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (string.IsNullOrEmpty(_currentPlugin.Magic)) { MessageBox.Show("当前插件无 magic。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (string.IsNullOrEmpty(_romsDir)) { MessageBox.Show("请先设置 ROM 目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dlg = new OpenFileDialog { Filter = "LST/文本 (*.lst;*.txt)|*.lst;*.txt|所有|*.*", FileName = "mame_cn.lst" };
            if (dlg.ShowDialog() != true) return;
            var mameDict = LoadMameCnLst(dlg.FileName);
            if (mameDict.Count == 0) { MessageBox.Show("未能解析 mame_cn.lst 或文件为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var romDir = Path.Combine(_romsDir, _currentPlugin.RomDir ?? "");
            if (!Directory.Exists(romDir)) { MessageBox.Show("ROM 目录不存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var fileTypes = (_currentPlugin.FileTypes ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToList();
            if (fileTypes.Count == 0) { MessageBox.Show("当前插件未配置 filetypes。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!_customTitles.ContainsKey(_currentPlugin.Magic)) _customTitles[_currentPlugin.Magic] = new Dictionary<string, string>(StringComparer.Ordinal);
            var section = _customTitles[_currentPlugin.Magic];
            int added = 0;
            foreach (var entry in Directory.EnumerateFileSystemEntries(romDir))
            {
                if (File.Exists(entry))
                {
                    var name = Path.GetFileName(entry).ToLowerInvariant();
                    if (fileTypes.Any(t => name.EndsWith(t.StartsWith(".") ? t : "." + t)))
                    {
                        var baseName = Path.GetFileNameWithoutExtension(entry);
                        var key = baseName.ToLowerInvariant();
                        if (mameDict.TryGetValue(key, out var cn))
                        { section[baseName] = cn; added++; }
                    }
                }
                else if (Directory.Exists(entry))
                {
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(entry))
                        {
                            var name = Path.GetFileName(f).ToLowerInvariant();
                            if (fileTypes.Any(t => name.EndsWith(t.StartsWith(".") ? t : "." + t)))
                            {
                                var baseName = Path.GetFileNameWithoutExtension(f);
                                var key = baseName.ToLowerInvariant();
                                if (mameDict.TryGetValue(key, out var cn))
                                { section[baseName] = cn; added++; }
                            }
                        }
                    }
                    catch { }
                }
            }
            _customTitlesModified = true;
            LoadRoms();
            LoadImages();
            if (MessageBox.Show("已从 mame_cn.lst 匹配并填入 " + added + " 条标题。是否保存到自定义标题文件？", "保存", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                SaveCustomTitles();
        }

        private static Dictionary<string, string> LoadMameCnLst(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var enc in new[] { Encoding.GetEncoding("gbk"), Encoding.UTF8, Encoding.GetEncoding("gb2312") })
            {
                try
                {
                    var lines = File.ReadAllLines(path, enc);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('\t');
                        if (parts.Length < 2) continue;
                        var romKey = parts[0].Trim().ToLowerInvariant();
                        var cn = parts[1].Trim();
                        if (!string.IsNullOrEmpty(romKey) && !string.IsNullOrEmpty(cn)) result[romKey] = cn;
                    }
                    return result;
                }
                catch { }
            }
            return result;
        }

        private void DgImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgImages.SelectedItem is ImageRow ir && _currentPlugin != null && !string.IsNullOrEmpty(_imagesDir))
            {
                var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
                var imagePath = Path.Combine(imageDir, ir.FileName);
                if (File.Exists(imagePath))
                {
                    try
                    {
                        // 从内存加载预览，避免长期占用文件导致调整大小时提示“另一进程正在使用”
                        byte[] bytes = File.ReadAllBytes(imagePath);
                        using (var ms = new MemoryStream(bytes))
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = ms;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            imgPreview.Source = bmp;
                        }
                    }
                    catch { imgPreview.Source = null; }
                }
                else imgPreview.Source = null;
            }
            else imgPreview.Source = null;
        }

        private void MenuEditPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (listPlugins.SelectedItem is PluginItem p && !string.IsNullOrEmpty(p.FilePath) && File.Exists(p.FilePath))
            {
                try { Process.Start("notepad.exe", "\"" + p.FilePath + "\""); }
                catch (Exception ex) { MessageBox.Show("打开失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void MenuDeletePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (listPlugins.SelectedItem is PluginItem p && !string.IsNullOrEmpty(p.FilePath) && File.Exists(p.FilePath))
            {
                if (MessageBox.Show("确定要删除插件 \"" + p.DisplayName + "\" 吗？文件将移动到 bak 目录。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                try
                {
                    var bakDir = Path.Combine(Path.GetDirectoryName(p.FilePath), "bak");
                    Directory.CreateDirectory(bakDir);
                    var bakPath = Path.Combine(bakDir, Path.GetFileName(p.FilePath));
                    File.Move(p.FilePath, bakPath);
                    _plugins.Remove(p);
                    if (_currentPlugin == p) { _currentPlugin = null; _romRows.Clear(); _imageRows.Clear(); txtRomCount.Text = "(0)"; txtMissingCover.Text = ""; txtImageCount.Text = "(0)"; }
                }
                catch (Exception ex) { MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void MenuOpenRomFolder_Click(object sender, RoutedEventArgs e)
        {
            if (dgRoms.SelectedItem is RomRow row && _currentPlugin != null && !string.IsNullOrEmpty(_romsDir))
            {
                var romDir = Path.Combine(_romsDir, _currentPlugin.RomDir ?? "");
                var folder = Path.GetFullPath(romDir);
                if (Directory.Exists(folder)) Process.Start("explorer.exe", "\"" + folder + "\"");
                else MessageBox.Show("文件夹不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MenuAddMissingCover_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlugin == null || string.IsNullOrEmpty(_imagesDir)) return;
            var selected = dgRoms.SelectedItems.Cast<RomRow>().Where(r => !r.HasImage).ToList();
            if (selected.Count == 0)
            {
                if (dgRoms.SelectedItem is RomRow r && r.HasImage)
                    MessageBox.Show("当前选中的 ROM 已有封面。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("请先选中缺少封面的 ROM（红色行）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            imageDir = Path.GetFullPath(imageDir);
            if (!Directory.Exists(imageDir))
            {
                try { Directory.CreateDirectory(imageDir); }
                catch (Exception ex) { MessageBox.Show("无法创建图片目录: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            }
            var dlg = new OpenFileDialog
            {
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
                Title = "选择封面图片"
            };
            if (dlg.ShowDialog() != true) return;
            var sourcePath = dlg.FileName;
            int added = 0;
            foreach (var row in selected)
            {
                var baseName = Path.GetFileNameWithoutExtension(row.FileName);
                var romExt = Path.GetExtension(row.FileName) ?? "";
                var targetFileName = baseName + romExt + ".png";
                var targetPath = Path.Combine(imageDir, targetFileName);
                try
                {
                    // 添加封面时统一保存为 PNG（非 PNG 源图自动转换）
                    using (var img = System.Drawing.Image.FromFile(sourcePath))
                    {
                        img.Save(targetPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    row.HasImage = true;
                    added++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("保存封面失败 " + targetFileName + ": " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                if (selected.Count > 1 && added < selected.Count)
                {
                    dlg = new OpenFileDialog { Filter = dlg.Filter, Title = "选择下一张封面 (" + (added + 1) + "/" + selected.Count + ")" };
                    if (dlg.ShowDialog() != true) break;
                    sourcePath = dlg.FileName;
                }
            }
            if (added > 0)
            {
                LoadImages();
                dgRoms.Items.Refresh();
                int missing = _romRows.Count(r => !r.HasImage);
                txtMissingCover.Text = missing > 0 ? L("  缺封面: " + missing, "  Missing covers: " + missing) : "";
                MessageBox.Show(L("已添加 " + added + " 张封面。", "Added " + added + " cover(s)."),
                                L("完成", "Done"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuOpenImageFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlugin == null || string.IsNullOrEmpty(_imagesDir)) return;
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            var folder = Path.GetFullPath(imageDir);
            if (Directory.Exists(folder)) Process.Start("explorer.exe", "\"" + folder + "\"");
            else MessageBox.Show("文件夹不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void MenuResizeImage_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgImages.SelectedItem is ImageRow ir) || _currentPlugin == null || string.IsNullOrEmpty(_imagesDir)) return;
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            var imagePath = Path.Combine(imageDir, ir.FileName);
            if (!File.Exists(imagePath)) return;
            // 先释放预览对文件的占用，避免“另一进程正在使用该文件”
            imgPreview.Source = null;
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(imagePath);
            }
            catch (Exception ex) { MessageBox.Show("无法读取图片: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            int origWidth, origHeight;
            try
            {
                using (var ms = new MemoryStream(fileBytes))
                using (var img = System.Drawing.Image.FromStream(ms))
                { origWidth = img.Width; origHeight = img.Height; }
            }
            catch { MessageBox.Show("无法解析图片尺寸。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var inputW = ShowInputDialog("输入新宽度(像素):", origWidth.ToString());
            if (inputW == null || !int.TryParse(inputW, out int width) || width < 50 || width > 4000) return;
            var inputH = ShowInputDialog("输入新高度(像素):", origHeight.ToString());
            if (inputH == null || !int.TryParse(inputH, out int height) || height < 50 || height > 4000) return;
            try
            {
                using (var ms = new MemoryStream(fileBytes))
                using (var bmp = new System.Drawing.Bitmap(ms))
                {
                    var newBmp = new System.Drawing.Bitmap(width, height);
                    using (var g = System.Drawing.Graphics.FromImage(newBmp))
                        g.DrawImage(bmp, 0, 0, width, height);
                    var tempPath = Path.Combine(imageDir, "temp_resize_" + ir.FileName);
                    newBmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                    newBmp.Dispose();
                }
                var bakPath = imagePath + ".bak";
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(imagePath, bakPath);
                File.Move(Path.Combine(imageDir, "temp_resize_" + ir.FileName), imagePath);
                File.Delete(bakPath);
                ir.Resolution = width + "×" + height;
                LoadImages();
                MessageBox.Show("图片已调整为 " + width + "×" + height + " 像素。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("调整失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void MenuRenameImage_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgImages.SelectedItem is ImageRow ir) || _currentPlugin == null || string.IsNullOrEmpty(_imagesDir)) return;
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            var imagePath = Path.Combine(imageDir, ir.FileName);
            if (!File.Exists(imagePath)) return;
            var newName = ShowInputDialog("输入新文件名:", ir.FileName);
            if (string.IsNullOrWhiteSpace(newName) || newName == ir.FileName) return;
            var newPath = Path.Combine(imageDir, newName);
            try
            {
                File.Move(imagePath, newPath);
                ir.FileName = newName;
                LoadImages();
            }
            catch (Exception ex) { MessageBox.Show("重命名失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void MenuConvertImage_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgImages.SelectedItem is ImageRow ir) || _currentPlugin == null || string.IsNullOrEmpty(_imagesDir)) return;
            var imageDir = Path.Combine(_imagesDir, _currentPlugin.CoverFolder ?? "");
            var imagePath = Path.Combine(imageDir, ir.FileName);
            if (!File.Exists(imagePath)) return;
            var formats = new[] { "PNG", "JPEG", "BMP", "GIF" };
            // 简单输入选择
            var choice = ShowInputDialog("输入格式 (PNG/JPEG/BMP/GIF):", "PNG");
            if (string.IsNullOrWhiteSpace(choice)) return;
            var format = formats.FirstOrDefault(f => f.Equals(choice.Trim(), StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(format)) { MessageBox.Show("未知格式。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            try
            {
                using (var img = System.Drawing.Image.FromFile(imagePath))
                {
                    var ext = "." + format.ToLowerInvariant();
                    if (ext == ".jpeg") ext = ".jpg";
                    var newPath = Path.Combine(imageDir, Path.GetFileNameWithoutExtension(ir.FileName) + ext);
                    var fmt = format.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Imaging.ImageFormat.Jpeg
                        : format.Equals("BMP", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Imaging.ImageFormat.Bmp
                        : format.Equals("GIF", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Imaging.ImageFormat.Gif
                        : System.Drawing.Imaging.ImageFormat.Png;
                    img.Save(newPath, fmt);
                    if (newPath != imagePath) { File.Delete(imagePath); ir.FileName = Path.GetFileName(newPath); }
                }
                LoadImages();
            }
            catch (Exception ex) { MessageBox.Show("转换失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            _languageZh = !_languageZh;
            var settings = SettingsManager.GetSettings();
            settings.UseEnglish = !_languageZh;
            SettingsManager.UpdateSettings(settings);
            ApplyLanguage();
        }

        private void Log(string message)
        {
            if (chkLog?.IsChecked != true || string.IsNullOrEmpty(_logPath)) return;
            try
            {
                File.AppendAllText(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        private string ShowInputDialog(string prompt, string defaultValue = "")
        {
            var w = new Window { Title = "输入", Width = 320, Height = 120, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
            var stack = new StackPanel { Margin = new Thickness(10) };
            stack.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 6) });
            var tb = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10) };
            stack.Children.Add(tb);
            var ok = new Button { Content = "确定", IsDefault = true, Width = 70, Margin = new Thickness(0, 0, 6, 0) };
            var cancel = new Button { Content = "取消", IsCancel = true, Width = 70 };
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            ok.Click += (s, _) => { w.DialogResult = true; w.Close(); };
            cancel.Click += (s, _) => w.Close();
            btnPanel.Children.Add(ok);
            btnPanel.Children.Add(cancel);
            stack.Children.Add(btnPanel);
            w.Content = stack;
            w.ShowActivated = true;
            return w.ShowDialog() == true ? tb.Text?.Trim() : null;
        }
    }
}
