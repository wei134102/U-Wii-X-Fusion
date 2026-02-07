using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// Wii U 游戏查询窗口：与 Wii 游戏数据库查询功能一致，数据来自 wiiutdb.xml 与 gametitle_wiiu.txt
    /// </summary>
    public partial class WiiUGameQueryWindow : Window
    {
        private WiiUGameDatabase _wiiuDatabase;
        private List<GameInfo> _allGames;
        private string _coverPath;
        private BitmapImage _currentCoverBitmap;

        public WiiUGameQueryWindow(string coverPath = "")
        {
            InitializeComponent();
            var icon = App.GetWindowIcon();
            if (icon != null) Icon = icon;
            _coverPath = coverPath;
            InitializeDatabase();
            SetupEventHandlers();
        }

        private void InitializeDatabase()
        {
            try
            {
                _wiiuDatabase = new WiiUGameDatabase();
                _wiiuDatabase.Initialize();

                _allGames = _wiiuDatabase.GetAllGames();
                dgGames.ItemsSource = _allGames;
                UpdateGameCount();

                PopulateGenreComboBox();
                PopulatePlatformComboBox();
                PopulateControllerComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化 Wii U 数据库时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupEventHandlers()
        {
            btnSearch.Click += BtnSearch_Click;
            btnClearSearch.Click += BtnClearSearch_Click;
            btnApplyFilters.Click += BtnApplyFilters_Click;
            cmbCoverType.SelectionChanged += CmbCoverType_SelectionChanged;
            dgGames.SelectionChanged += DgGames_SelectionChanged;
        }

        private void PopulateGenreComboBox()
        {
            var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in _allGames)
            {
                if (game.Genres != null)
                {
                    foreach (var genre in game.Genres)
                        if (!string.IsNullOrWhiteSpace(genre)) genres.Add(genre);
                }
            }
            foreach (var genre in genres.OrderBy(g => g))
            {
                cmbGenre.Items.Add(genre);
            }
        }

        private void PopulatePlatformComboBox()
        {
            var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in _allGames)
            {
                if (!string.IsNullOrWhiteSpace(game.PlatformType))
                    platforms.Add(game.PlatformType);
                else if (!string.IsNullOrWhiteSpace(game.Platform))
                    platforms.Add(game.Platform);
            }
            foreach (var p in platforms.OrderBy(x => x))
            {
                cmbPlatform.Items.Add(p);
            }
        }

        private void PopulateControllerComboBox()
        {
            var controllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in _allGames)
            {
                if (game.Controllers != null)
                {
                    foreach (var c in game.Controllers)
                        if (!string.IsNullOrWhiteSpace(c)) controllers.Add(c);
                }
            }
            foreach (var c in controllers.OrderBy(x => x))
            {
                cmbController.Items.Add(c);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            try { RefreshList(); }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            RefreshList();
        }

        private static string GetFilterComboValue(ComboBox cmb)
        {
            var item = cmb.SelectedItem;
            if (item == null) return null;
            if (item is ComboBoxItem cbi)
                return cbi.Content?.ToString();
            return item.ToString();
        }

        private void GetCurrentFilter(out string genre, out string platformType, out int? players, out string controller, out string region)
        {
            genre = GetFilterComboValue(cmbGenre);
            platformType = GetFilterComboValue(cmbPlatform);
            string playersText = GetFilterComboValue(cmbPlayers);
            controller = GetFilterComboValue(cmbController);
            region = GetFilterComboValue(cmbRegion);

            if (genre == "全部游戏类型") genre = null;
            if (platformType == "全部平台") platformType = null;
            players = null;
            if (!string.IsNullOrEmpty(playersText) && playersText != "全部人数" && playersText.EndsWith("人"))
            {
                string num = playersText.TrimEnd('人').Trim();
                if (int.TryParse(num, out int p) && p > 0)
                    players = p;
            }
            if (controller == "全部控制器") controller = null;
            if (region == "全部区域") region = null;
        }

        private void RefreshList()
        {
            var query = txtSearch.Text.Trim();
            IEnumerable<GameInfo> baseList = string.IsNullOrEmpty(query)
                ? _allGames
                : _wiiuDatabase.SearchGames(query);

            GetCurrentFilter(out string genre, out string platformType, out int? players, out string controller, out string region);

            var filtered = _wiiuDatabase.FilterGameList(baseList,
                genre: genre,
                language: null,
                controller: controller,
                region: region,
                platformType: platformType,
                players: players);

            dgGames.ItemsSource = filtered;
            UpdateGameCount();
        }

        private void BtnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            try { RefreshList(); }
            catch (Exception ex)
            {
                MessageBox.Show($"应用筛选时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateGameCount()
        {
            int count = 0;
            if (dgGames?.ItemsSource is ICollection col)
                count = col.Count;
            else if (dgGames?.ItemsSource is IEnumerable en)
                count = en.Cast<object>().Count();
            txtGameCount.Text = $"共 {count} 个游戏";
        }

        private void DgGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgGames.SelectedItem is GameInfo selectedGame)
            {
                DisplayGameDetails(selectedGame);
            }
        }

        private void DisplayGameDetails(GameInfo game)
        {
            spGameDetails.Children.Clear();

            spGameDetails.Children.Add(new TextBlock { Text = $"游戏ID: {game.GameId}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"游戏名称: {game.Title}", Margin = new Thickness(0, 0, 0, 5) });

            if (!string.IsNullOrEmpty(game.ChineseTitle))
            {
                spGameDetails.Children.Add(new TextBlock { Text = $"中文名称: {game.ChineseTitle}", Margin = new Thickness(0, 0, 0, 5) });
            }

            spGameDetails.Children.Add(new TextBlock { Text = $"平台: {game.Platform}", Margin = new Thickness(0, 0, 0, 5) });
            if (!string.IsNullOrEmpty(game.PlatformType))
                spGameDetails.Children.Add(new TextBlock { Text = $"平台类型: {game.PlatformType}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"区域: {game.Region}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"玩家数量: {game.Players}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"发行商: {game.Publisher}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"开发商: {game.Developer}", Margin = new Thickness(0, 0, 0, 5) });

            if (game.Controllers != null && game.Controllers.Any())
            {
                spGameDetails.Children.Add(new TextBlock { Text = "控制器: " + string.Join(", ", game.Controllers), Margin = new Thickness(0, 0, 0, 5) });
            }

            if (game.Genres != null && game.Genres.Any())
            {
                spGameDetails.Children.Add(new TextBlock { Text = "游戏类型: " + string.Join(", ", game.Genres), Margin = new Thickness(0, 0, 0, 5) });
            }

            if (game.Languages != null && game.Languages.Any())
            {
                spGameDetails.Children.Add(new TextBlock { Text = "支持语言: " + string.Join(", ", game.Languages), Margin = new Thickness(0, 0, 0, 5) });
            }

            if (!string.IsNullOrEmpty(game.Synopsis))
            {
                spGameDetails.Children.Add(new TextBlock { Text = "简介:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 5) });
                spGameDetails.Children.Add(new TextBlock { Text = game.Synopsis, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) });
            }

            LoadGameCover(game);
        }

        #region 封面相关方法

        private void CmbCoverType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgGames.SelectedItem is GameInfo selectedGame)
            {
                LoadGameCover(selectedGame);
            }
        }

        private void LoadGameCover(GameInfo game)
        {
            if (game == null)
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "请选择游戏查看封面");
                return;
            }

            if (string.IsNullOrEmpty(_coverPath))
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "请先在设置中设置 Wii U 封面存储路径（或通用封面路径）");
                return;
            }

            string coverType = GetCoverTypeString();
            string baseName = game.GameId;
            // 先查 png，查不到再查 jpg（包含 jpg 的查询）
            string coverPath = ResolveCoverPath(coverType, baseName, ".png")
                ?? ResolveCoverPath(coverType, baseName, ".jpg");

            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(coverPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    string ext = Path.GetExtension(coverPath).ToLowerInvariant();
                    UpdateCoverPreview(bitmap, $"已加载 {coverType} 封面 ({ext.TrimStart('.')})");
                    _currentCoverBitmap = bitmap;
                }
                catch (Exception ex)
                {
                    _currentCoverBitmap = null;
                    UpdateCoverPreview(null, $"加载封面失败: {ex.Message}");
                }
            }
            else
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, $"未找到 {coverType} 封面（已尝试 png、jpg）");
            }
        }

        /// <summary>解析封面路径：先按子目录（如 2d/3d/disc/full），再尝试 Full 目录；返回第一个存在的路径或 null。</summary>
        private string ResolveCoverPath(string coverType, string baseName, string extension)
        {
            string fileName = baseName + extension;
            string path = Path.Combine(_coverPath, coverType, fileName);
            if (File.Exists(path)) return path;
            if (string.Equals(coverType, "full", StringComparison.OrdinalIgnoreCase))
                return null;
            path = Path.Combine(_coverPath, "Full", fileName);
            return File.Exists(path) ? path : null;
        }

        private string GetCoverTypeString()
        {
            var item = cmbCoverType.SelectedItem;
            if (item is ComboBoxItem cbi)
                return (cbi.Content?.ToString() ?? "2D").ToLowerInvariant();
            return (item?.ToString() ?? "2D").ToLowerInvariant();
        }

        private void UpdateCoverPreview(BitmapImage bitmap, string status)
        {
            imgCover.Source = bitmap;
            txtCoverStatus.Text = status;
        }

        private void ImgCover_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentCoverBitmap == null) return;
            var viewer = new CoverImageViewerWindow(_currentCoverBitmap) { Owner = this };
            viewer.Show();
        }

        #endregion
    }
}
