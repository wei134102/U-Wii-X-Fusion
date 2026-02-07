using System;
using System.IO;
using System.Web.Script.Serialization;

namespace U_Wii_X_Fusion.Core.Settings
{
    public class AppSettings
    {
        // 通用设置
        public bool AutoUpdate { get; set; } = true;
        public bool EnableLogging { get; set; } = false;
        public bool CheckDevices { get; set; } = true;

        // 路径设置
        public string GamePath { get; set; } = string.Empty;
        public string DatabasePath { get; set; } = string.Empty;
        public string CoverPath { get; set; } = string.Empty;
        /// <summary>Wii U 封面存储路径（不填则使用 CoverPath）</summary>
        public string WiiUCoverPath { get; set; } = string.Empty;
        /// <summary>上次扫描的游戏目录（Wii/NGC）</summary>
        public string LastScanPath { get; set; } = string.Empty;
        /// <summary>Xbox 360 游戏目录（如 E:\roms\xbox360\X360K）</summary>
        public string LastXboxScanPath { get; set; } = @"E:\roms\xbox360\X360K";

        // 网络设置
        public string ApiKey { get; set; } = string.Empty;
        public bool EnableProxy { get; set; } = false;

        // 更新检查（缓存/限流保护）
        /// <summary>上次进行 GitHub 更新检查的时间（UTC）。用于减少请求次数，避免触发 GitHub API 限流。</summary>
        public DateTime? LastUpdateCheckUtc { get; set; }

        /// <summary>最近一次成功获取到的 release 信息缓存时间（UTC）。</summary>
        public DateTime? LatestReleaseCachedAtUtc { get; set; }
        public string CachedLatestVersion { get; set; } = string.Empty;
        public string CachedLatestTagName { get; set; } = string.Empty;
        public string CachedLatestName { get; set; } = string.Empty;
        public string CachedLatestBody { get; set; } = string.Empty;
        public string CachedLatestPublishedAt { get; set; } = string.Empty;
        public string CachedLatestDownloadUrl { get; set; } = string.Empty;
        public long CachedLatestDownloadSize { get; set; } = 0;
    }

    public class SettingsManager
    {
        // 保存到程序所在目录下的 CONFIG 文件夹，便于管理
        private static readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CONFIG", "settings.json");
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static AppSettings _settings;

        static SettingsManager()
        {
            LoadSettings();
        }

        public static AppSettings GetSettings()
        {
            return _settings;
        }

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _settings = _serializer.Deserialize<AppSettings>(json);
                }
                else
                {
                    // 使用默认设置
                    _settings = new AppSettings();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载设置时出错: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = _serializer.Serialize(_settings);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存设置时出错: {ex.Message}");
            }
        }

        public static void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
            SaveSettings();
        }
    }
}