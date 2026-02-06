using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using U_Wii_X_Fusion.Core.Update;

namespace U_Wii_X_Fusion
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateService _updateService;
        private ReleaseInfo _latestRelease;
        private string _currentVersion;

        public UpdateWindow(UpdateService updateService, string currentVersion)
        {
            InitializeComponent();
            _updateService = updateService;
            _currentVersion = currentVersion;
            Loaded += UpdateWindow_Loaded;
        }

        private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtCurrentVersion.Text = $"当前版本: {_currentVersion}";
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                _latestRelease = await _updateService.GetLatestReleaseAsync();
                txtLatestVersion.Text = $"最新版本: {_latestRelease.Version}";
                
                if (_updateService.IsNewerVersion(_latestRelease.Version, _currentVersion))
                {
                    txtStatus.Text = $"发现新版本 {_latestRelease.Version}！";
                    txtStatus.Foreground = System.Windows.Media.Brushes.Green;
                    btnDownload.IsEnabled = true;
                    
                    if (!string.IsNullOrEmpty(_latestRelease.Body))
                    {
                        txtReleaseNotes.Text = "更新内容：\n" + _latestRelease.Body;
                        txtReleaseNotes.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    txtStatus.Text = "当前已是最新版本。";
                    txtStatus.Foreground = System.Windows.Media.Brushes.Gray;
                    btnDownload.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"检查更新失败: {ex.Message}";
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                btnDownload.IsEnabled = false;
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_latestRelease == null || string.IsNullOrEmpty(_latestRelease.DownloadUrl))
            {
                MessageBox.Show("无法获取下载地址。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnDownload.IsEnabled = false;
            btnCancel.IsEnabled = false;
            pbDownload.Visibility = Visibility.Visible;
            pbDownload.Value = 0;

            try
            {
                var progress = new Progress<int>(p => pbDownload.Value = p);
                string zipPath = await _updateService.DownloadUpdateAsync(_latestRelease.DownloadUrl, progress);
                string extractDir = _updateService.ExtractUpdate(zipPath);
                
                string targetDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                
                _updateService.CreateUpdateScript(extractDir, targetDir, exePath);
                
                MessageBox.Show("更新文件已下载。程序将在关闭后自动更新并重启。\n请点击【关闭】按钮完成更新。",
                    "更新准备完成", MessageBoxButton.OK, MessageBoxImage.Information);
                
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载或应用更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                btnDownload.IsEnabled = true;
                btnCancel.IsEnabled = true;
                pbDownload.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
