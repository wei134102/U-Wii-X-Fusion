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
    /// WiiGameQueryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WiiGameQueryWindow : Window
    {
        private WiiGameDatabase _wiiDatabase;
        private List<GameInfo> _allGames;
        private string _coverPath;
        private BitmapImage _currentCoverBitmap;

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
                UpdateGameCount();

                // 填充游戏类型、平台、控制器下拉框
                PopulateGenreComboBox();
                PopulatePlatformComboBox();
                PopulateControllerComboBox();
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
                foreach (var c in game.Controllers)
                {
                    if (!string.IsNullOrWhiteSpace(c))
                        controllers.Add(c);
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

        /// <summary>从 ComboBox 取得用于筛选的显示值（ComboBoxItem 取 Content，否则取 ToString）</summary>
        private static string GetFilterComboValue(ComboBox cmb)
        {
            var item = cmb.SelectedItem;
            if (item == null) return null;
            if (item is ComboBoxItem cbi)
                return cbi.Content?.ToString();
            return item.ToString();
        }

        /// <summary>获取当前筛选条件（与界面下拉框一致）</summary>
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

        /// <summary>先按搜索框得到基础列表，再应用当前筛选条件，使搜索与筛选同时生效</summary>
        private void RefreshList()
        {
            var query = txtSearch.Text.Trim();
            IEnumerable<GameInfo> baseList = string.IsNullOrEmpty(query)
                ? _allGames
                : _wiiDatabase.SearchGames(query);

            GetCurrentFilter(out string genre, out string platformType, out int? players, out string controller, out string region);

            var filtered = _wiiDatabase.FilterGameList(baseList,
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
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "请选择游戏查看封面");
                return;
            }

            if (string.IsNullOrEmpty(_coverPath))
            {
                _currentCoverBitmap = null;
                UpdateCoverPreview(null, "请先在设置中设置封面存储路径");
                return;
            }

            string coverType = GetCoverTypeString();
            string coverFileName = $"{game.GameId}.png";
            string coverPath = System.IO.Path.Combine(_coverPath, coverType, coverFileName);
            // 部分封面包使用 "Full" 等首字母大写目录名
            if (!File.Exists(coverPath) && coverType == "full")
                coverPath = System.IO.Path.Combine(_coverPath, "Full", coverFileName);

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
                UpdateCoverPreview(null, $"未找到 {coverType} 封面");
            }
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

    /// <summary>封面大图查看：居中、原图大小、滚轮缩放时窗口跟随放大缩小、右键关闭</summary>
    public class CoverImageViewerWindow : Window
    {
        private readonly Image _image;
        private readonly ScaleTransform _scaleTransform;
        private double _scale = 1.0;
        private readonly double _baseWidth;
        private readonly double _baseHeight;
        private const double PaddingW = 20;
        private const double PaddingH = 40;
        private static readonly double MaxW = SystemParameters.PrimaryScreenWidth * 0.95;
        private static readonly double MaxH = SystemParameters.PrimaryScreenHeight * 0.95;

        public CoverImageViewerWindow(BitmapImage bitmap)
        {
            Title = "封面预览";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.CanResize;
            Background = Brushes.Black;

            _baseWidth = bitmap.PixelWidth;
            _baseHeight = bitmap.PixelHeight;
            _scale = 1.0;
            _scaleTransform = new ScaleTransform(1, 1);
            _image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.None,
                RenderTransform = _scaleTransform,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
            };

            // 始终以原始尺寸显示，窗口按原图大小（超出屏幕时出现滚动条）
            ApplyWindowSize();

            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _image
            };

            _image.MouseWheel += (s, e) =>
            {
                e.Handled = true;
                double delta = e.Delta > 0 ? 1.15 : 1 / 1.15;
                _scale = Math.Max(0.25, Math.Min(8, _scale * delta));
                _scaleTransform.ScaleX = _scaleTransform.ScaleY = _scale;
                ApplyWindowSize();
            };
            _image.MouseRightButtonDown += (s, e) => Close();
            _image.Cursor = System.Windows.Input.Cursors.SizeAll;
        }

        private void ApplyWindowSize()
        {
            double w = Math.Min(_baseWidth * _scale + PaddingW, MaxW);
            double h = Math.Min(_baseHeight * _scale + PaddingH, MaxH);
            Width = Math.Max(120, w);
            Height = Math.Max(100, h);
        }
    }
}