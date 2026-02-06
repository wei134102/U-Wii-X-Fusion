using System.Configuration;

namespace U_Wii_X_Fusion.Settings.Models
{
    public class AppSettings
    {
        // 通用设置
        public bool AutoUpdate { get; set; }
        public bool EnableLogging { get; set; }
        public bool CheckDevicesOnStartup { get; set; }

        // 路径设置
        public string GameStoragePath { get; set; }
        public string DatabasePath { get; set; }
        public string MetadataCachePath { get; set; }

        // 网络设置
        public string MetadataApiKey { get; set; }
        public bool EnableProxy { get; set; }
        public string ProxyAddress { get; set; }
        public int ProxyPort { get; set; }

        // 界面设置
        public bool UseDarkMode { get; set; }
        public bool ShowTooltips { get; set; }
        public bool AutoRefreshGameList { get; set; }

        // 传输设置
        public int TransferChunkSize { get; set; }
        public bool EnableTransferCompression { get; set; }
        public int TransferTimeout { get; set; }

        public AppSettings()
        {
            // 默认值
            AutoUpdate = true;
            EnableLogging = true;
            CheckDevicesOnStartup = true;

            GameStoragePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "U-Wii-X Fusion", "Games");
            DatabasePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "U-Wii-X Fusion", "Database");
            MetadataCachePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "U-Wii-X Fusion", "Metadata");

            MetadataApiKey = string.Empty;
            EnableProxy = false;
            ProxyAddress = "localhost";
            ProxyPort = 8080;

            UseDarkMode = false;
            ShowTooltips = true;
            AutoRefreshGameList = true;

            TransferChunkSize = 1024 * 1024; // 1MB
            EnableTransferCompression = false;
            TransferTimeout = 300; // 5分钟
        }

        public void Save()
        {
            // 这里应该实现保存设置到配置文件的逻辑
            // 暂时只做模拟
        }

        public void Load()
        {
            // 这里应该实现从配置文件加载设置的逻辑
            // 暂时只使用默认值
        }
    }
}
