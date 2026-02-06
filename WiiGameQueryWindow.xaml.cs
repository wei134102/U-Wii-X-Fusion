using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// WiiGameQueryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WiiGameQueryWindow : Window
    {
        private WiiGameDatabase _wiiDatabase;
        private List<GameInfo> _allGames;
        private string _coverPath;

        public WiiGameQueryWindow(string coverPath = "")
        {
            InitializeComponent();
            _coverPath = coverPath;
            InitializeDatabase();
            SetupEventHandlers();
        }

        private void InitializeDatabase()
        {
            try
            {
                // 初始化Wii游戏数据库
                _wiiDatabase = new WiiGameDatabase();
                _wiiDatabase.Initialize();
                
                // 加载所有游戏
                _allGames = _wiiDatabase.GetAllGames();
                dgGames.ItemsSource = _allGames;
                
                // 填充游戏类型下拉框
                PopulateGenreComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化数据库时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            // 收集所有游戏类型
            var genres = new HashSet<string>();
            foreach (var game in _allGames)
            {
                foreach (var genre in game.Genres)
                {
                    genres.Add(genre);
                }
            }

            // 添加到下拉框
            foreach (var genre in genres.OrderBy(g => g))
            {
                cmbGenre.Items.Add(genre);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var query = txtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    var results = _wiiDatabase.SearchGames(query);
                    dgGames.ItemsSource = results;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            dgGames.ItemsSource = _allGames;
        }

        private void BtnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var genre = cmbGenre.SelectedItem?.ToString();
                var language = cmbLanguage.SelectedItem?.ToString();
                var controller = cmbController.SelectedItem?.ToString();
                var region = cmbRegion.SelectedItem?.ToString();

                var filteredGames = _wiiDatabase.FilterGames(
                    genre: genre,
                    language: language,
                    controller: controller,
                    region: region
                );

                dgGames.ItemsSource = filteredGames;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用筛选时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            // 清空现有内容
            spGameDetails.Children.Clear();

            // 添加游戏详情
            spGameDetails.Children.Add(new TextBlock { Text = $"游戏ID: {game.GameId}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"游戏名称: {game.Title}", Margin = new Thickness(0, 0, 0, 5) });
            
            if (!string.IsNullOrEmpty(game.ChineseTitle))
            {
                spGameDetails.Children.Add(new TextBlock { Text = $"中文名称: {game.ChineseTitle}", Margin = new Thickness(0, 0, 0, 5) });
            }
            
            spGameDetails.Children.Add(new TextBlock { Text = $"平台: {game.Platform}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"平台类型: {game.PlatformType}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"区域: {game.Region}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"玩家数量: {game.Players}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"发行商: {game.Publisher}", Margin = new Thickness(0, 0, 0, 5) });
            spGameDetails.Children.Add(new TextBlock { Text = $"开发商: {game.Developer}", Margin = new Thickness(0, 0, 0, 5) });

            if (game.Controllers.Any())
            {
                spGameDetails.Children.Add(new TextBlock { Text = "控制器: " + string.Join(", ", game.Controllers), Margin = new Thickness(0, 0, 0, 5) });
            }

            if (game.Genres.Any())
            {
                spGameDetails.Children.Add(new TextBlock { Text = "游戏类型: " + string.Join(", ", game.Genres), Margin = new Thickness(0, 0, 0, 5) });
            }

            if (game.Languages.Any())
            {
                spGameDetails.Children.Add(new TextBlock { Text = "支持语言: " + string.Join(", ", game.Languages), Margin = new Thickness(0, 0, 0, 5) });
            }
            
            // 加载游戏封面
            LoadGameCover(game);
        }

        #region 封面相关方法

        private void CmbCoverType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 当封面类型变化时，更新当前选中游戏的封面
            if (dgGames.SelectedItem is GameInfo selectedGame)
            {
                LoadGameCover(selectedGame);
            }
        }

        private void LoadGameCover(GameInfo game)
        {
            if (game == null)
            {
                UpdateCoverPreview(null, "请选择游戏查看封面");
                return;
            }

            if (string.IsNullOrEmpty(_coverPath))
            {
                UpdateCoverPreview(null, "请先在设置中设置封面存储路径");
                return;
            }

            string coverType = cmbCoverType.SelectedItem?.ToString() ?? "2D";
            string coverFileName = $"{game.GameId}.png";
            string coverPath = System.IO.Path.Combine(_coverPath, coverType.ToLower(), coverFileName);

            if (File.Exists(coverPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(coverPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // 冻结以在UI线程中使用

                    UpdateCoverPreview(bitmap, $"已加载 {coverType} 封面");
                }
                catch (Exception ex)
                {
                    UpdateCoverPreview(null, $"加载封面失败: {ex.Message}");
                }
            }
            else
            {
                UpdateCoverPreview(null, $"未找到 {coverType} 封面");
            }
        }

        private void UpdateCoverPreview(BitmapImage bitmap, string status)
        {
            imgCover.Source = bitmap;
            txtCoverStatus.Text = status;
        }

        #endregion
    }
}