using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using U_Wii_X_Fusion.Core.Settings;
using U_Wii_X_Fusion.Core.Update;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        /// <summary>主题名称到 XAML 文件路径的映射</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> ThemeMap =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Default", "Themes/DefaultTheme.xaml" },
                { "Blue", "Themes/BlueTheme.xaml" },
                { "Green", "Themes/GreenTheme.xaml" },
                { "Purple", "Themes/PurpleTheme.xaml" },
                { "Orange", "Themes/OrangeTheme.xaml" },
                { "Cyan", "Themes/CyanTheme.xaml" }
            };

        /// <summary>当前主题名称</summary>
        public static string CurrentTheme { get; private set; } = "Default";

        /// <summary>切换主题（运行时动态替换资源字典）</summary>
        public static void SwitchTheme(string themeName)
        {
            if (!ThemeMap.ContainsKey(themeName))
                themeName = "Default";

            var source = new Uri(ThemeMap[themeName], UriKind.Relative);

            // 移除旧的主题字典
            var oldDict = Current.Resources.MergedDictionaries.Count > 0
                ? Current.Resources.MergedDictionaries[0]
                : null;
            if (oldDict != null)
                Current.Resources.MergedDictionaries.Remove(oldDict);

            // 添加新的主题字典（必须在索引0，以便样式中的 StaticResource 能正确解析）
            var newDict = new ResourceDictionary { Source = source };
            Current.Resources.MergedDictionaries.Insert(0, newDict);

            CurrentTheme = themeName;
        }

        /// <summary>从程序目录 icons 下加载窗口图标（优先 icon.ico，其次 icon.png），供主窗口和子窗口使用。</summary>
        public static System.Windows.Media.ImageSource GetWindowIcon()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string iconPath = Path.Combine(baseDir, "icons", "icon.ico");
                if (!File.Exists(iconPath))
                    iconPath = Path.Combine(baseDir, "icons", "icon.png");
                if (File.Exists(iconPath))
                    return BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
            }
            catch { }
            return null;
        }

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
