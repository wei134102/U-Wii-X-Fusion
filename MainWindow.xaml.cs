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
using U_Wii_X_Fusion.Core.Settings;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private WiiGameDatabase _wiiDatabase;
        private List<GameInfo> _allGames;
        private string _coverPath;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            SetupEventHandlers();
            LoadSettings();
            LoadCoverPath();
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化数据库时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupEventHandlers()
        {
            btnGameQuery.Click += BtnGameQuery_Click;
            btnSaveSettings.Click += BtnSaveSettings_Click;
            btnBrowseCoverPath.Click += BtnBrowseCoverPath_Click;
            btnBrowseGamePath.Click += BtnBrowseGamePath_Click;
            btnBrowseDatabasePath.Click += BtnBrowseDatabasePath_Click;
        }

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

        private void btnGameQuery_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
