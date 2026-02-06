using System;
using System.Threading.Tasks;
using System.Windows;
using U_Wii_X_Fusion.Core.Settings;
using U_Wii_X_Fusion.Core.Update;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 如果启用了自动更新，在后台检查更新（不阻塞启动）
            var settings = SettingsManager.GetSettings();
            if (settings.AutoUpdate)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000); // 延迟3秒，让主窗口先显示

                        // 限流保护：启动时不要频繁打 GitHub API
                        try
                        {
                            var s = SettingsManager.GetSettings();
                            if (s.LastUpdateCheckUtc.HasValue)
                            {
                                var age = DateTime.UtcNow - s.LastUpdateCheckUtc.Value;
                                if (age < TimeSpan.FromHours(6))
                                    return;
                            }
                        }
                        catch { /* ignore */ }

                        var updateService = new UpdateService("wei134102", "U-Wii-X-Fusion");
                        string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
                        var latest = await updateService.GetLatestReleaseAsync();
                        
                        if (updateService.IsNewerVersion(latest.Version, currentVersion))
                        {
                            // 在UI线程显示更新窗口
                            Dispatcher.Invoke(() =>
                            {
                                var updateWindow = new UpdateWindow(updateService, currentVersion);
                                updateWindow.ShowDialog();
                            });
                        }
                    }
                    catch
                    {
                        // 静默失败，不影响程序启动
                    }
                });
            }
        }
    }
}
