using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using U_Wii_X_Fusion.Core;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion
{
    public partial class Xbox360GameQueryWindow : Window
    {
        private Xbox360TitlesDatabase _database;
        private List<GameInfo> _allGames;
        private readonly string _coverPath;
        private BitmapImage _currentCoverBitmap;

        public Xbox360GameQueryWindow(string coverPath = "")
        {
            InitializeComponent();
            var icon = App.GetWindowIcon();
            if (icon != null) Icon = icon;
            _coverPath = coverPath ?? string.Empty;
            _database = new Xbox360TitlesDatabase();
            _database.Initialize();
            _allGames = _database.GetAllGames();
            LoadCategoryFilter();
            RefreshList();
        }

        private void LoadCategoryFilter()
        {
            var categories = _database.GetCategories();
            cmbCategory.Items.Clear();
            cmbCategory.Items.Add("全部");
            foreach (var c in categories)
                cmbCategory.Items.Add(c);
            cmbCategory.SelectedIndex = 0;
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            cmbCategory.SelectedIndex = 0;
            RefreshList();
        }

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshList();
        }

        private void RefreshList()
        {
            var q = txtSearch?.Text?.Trim() ?? string.Empty;
            var list = string.IsNullOrEmpty(q) ? _allGames : _database.SearchGames(q);
            var selCat = cmbCategory?.SelectedItem as string;
            if (!string.IsNullOrEmpty(selCat) && selCat != "全部")
                list = list.Where(g => !string.IsNullOrEmpty(g.Category) && g.Category.Split(',').Select(s => s.Trim()).Contains(selCat, StringComparer.OrdinalIgnoreCase)).ToList();
            dgGames.ItemsSource = list;
            UpdateGameCount();
        }

        private void UpdateGameCount()
        {
            int count = 0;
            if (dgGames?.ItemsSource is IEnumerable<GameInfo> en)
                count = en.Count();
            txtGameCount.Text = $"共 {count} 个游戏";
        }

        private void DgGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            spGameDetails.Children.Clear();
            if (dgGames.SelectedItem is GameInfo g)
            {
                LoadGameCover(g);
                spGameDetails.Children.Add(new TextBlock { Text = $"游戏ID: {g.GameId}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
                spGameDetails.Children.Add(new TextBlock { Text = $"游戏名称: {g.Title ?? ""}", Margin = new Thickness(0, 0, 0, 6) });
                spGameDetails.Children.Add(new TextBlock { Text = $"中文名称: {g.ChineseTitle ?? ""}", Margin = new Thickness(0, 0, 0, 6) });
                if (!string.IsNullOrEmpty(g.Category))
                    spGameDetails.Children.Add(new TextBlock { Text = $"分类: {g.Category}", Margin = new Thickness(0, 0, 0, 6) });
                if (!string.IsNullOrEmpty(g.Synopsis))
                {
                    spGameDetails.Children.Add(new TextBlock { Text = "简介:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) });
                    var syn = new TextBlock { Text = g.Synopsis, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
                    spGameDetails.Children.Add(syn);
                }
            }
            else
            {
                LoadGameCover(null);
            }
        }

        private async void BtnDownloadCover_Click(object sender, RoutedEventArgs e)
        {
            var game = dgGames.SelectedItem as GameInfo;
            if (game == null || string.IsNullOrWhiteSpace(game.GameId))
            {
                MessageBox.Show("请先选择一个游戏。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(_coverPath) || !Directory.Exists(_coverPath))
            {
                MessageBox.Show("请先在设置中配置 Xbox 360 封面路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string titleId = game.GameId.Trim().ToUpperInvariant();
            string xboxDir = Path.Combine(_coverPath, "xbox");
            string savePath = Path.Combine(xboxDir, titleId + ".png");

            txtCoverStatus.Text = "正在下载封面...";
            btnDownloadCover.IsEnabled = false;
            try
            {
                bool success = await System.Threading.Tasks.Task.Run(() =>
                    Xbox360CoverDownloader.DownloadCover(titleId, savePath));

                if (success)
                {
                    LoadGameCover(game); // 复用现有预览刷新逻辑
                    MessageBox.Show("封面下载完成。", "下载封面", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    txtCoverStatus.Text = "下载失败：未获取到可用封面";
                    MessageBox.Show("封面下载失败：未获取到可用封面。", "下载封面", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                txtCoverStatus.Text = "下载失败";
                MessageBox.Show($"封面下载失败：{ex.Message}", "下载封面", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                btnDownloadCover.IsEnabled = true;
            }
        }

        private void LoadGameCover(GameInfo game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.GameId))
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "请选择游戏查看封面");
                return;
            }
            if (string.IsNullOrWhiteSpace(_coverPath) || !Directory.Exists(_coverPath))
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "请先在设置中配置 Xbox 360 封面路径");
                return;
            }

            string id = game.GameId.Trim().ToUpperInvariant();
            var candidates = BuildCoverCandidates(id);
            string hit = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(hit))
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "未找到封面（已尝试 png/jpg 及常见子目录）");
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(hit);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                _currentCoverBitmap = bitmap;
                UpdateCoverPreview(bitmap, $"已加载封面：{Path.GetFileName(hit)}");
            }
            catch (Exception ex)
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, $"封面加载失败：{ex.Message}");
            }
        }

        private List<string> BuildCoverCandidates(string gameId)
        {
            var names = new[]
            {
                gameId + ".png", gameId + ".jpg", gameId + ".jpeg",
                gameId.ToLowerInvariant() + ".png", gameId.ToLowerInvariant() + ".jpg", gameId.ToLowerInvariant() + ".jpeg"
            };
            var dirs = new[]
            {
                _coverPath,
                Path.Combine(_coverPath, "2d"),
                Path.Combine(_coverPath, "3d"),
                Path.Combine(_coverPath, "disc"),
                Path.Combine(_coverPath, "full"),
                Path.Combine(_coverPath, "Full"),
                Path.Combine(_coverPath, "xbox"),
                Path.Combine(_coverPath, "xbox360")
            };

            var result = new List<string>();
            foreach (var d in dirs)
            {
                foreach (var n in names)
                    result.Add(Path.Combine(d, n));
            }
            return result;
        }

        private void UpdateCoverPreview(BitmapImage bitmap, string status)
        {
            if (imgCover != null) imgCover.Source = bitmap;
            if (txtCoverStatus != null) txtCoverStatus.Text = status ?? string.Empty;
        }

        private void ImgCover_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentCoverBitmap == null) return;
            var viewer = new CoverImageViewerWindow(_currentCoverBitmap) { Owner = this };
            viewer.Show();
        }
    }
}
