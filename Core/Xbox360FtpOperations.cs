// Xbox 360 FTP 连接（参考 AuroraAssetEditor FTPOperations）
// 用于连接运行 Aurora 的 Xbox 360 主机 FTP，访问 /Game/Data/GameData/ 与 Content.db

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Web.Script.Serialization;

namespace U_Wii_X_Fusion.Core
{
    /// <summary>连接 Xbox 360（Aurora）FTP：测试连接、保存设置、浏览 GameData、下载 Content.db 等。</summary>
    public class Xbox360FtpOperations
    {
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private FtpClient _client;
        private FtpSettings _settings;

        public Xbox360FtpOperations()
        {
            LoadSettings();
        }

        /// <summary>状态变化时触发，参数为状态文本。</summary>
        public event EventHandler<string> StatusChanged;

        public string IpAddress => _settings?.IpAddress ?? "";
        public string Username => _settings?.Username ?? "";
        public string Password => _settings?.Password ?? "";
        public string Port => _settings?.Port ?? "21";
        public bool HaveSettings => _settings?.Loaded ?? false;

        public bool ConnectionEstablished
        {
            get
            {
                if (_client != null && _client.IsConnected)
                    return true;
                try { MakeConnection(); }
                catch { }
                return _client != null && _client.IsConnected;
            }
        }

        private void SendStatus(string msg, params object[] args)
        {
            StatusChanged?.Invoke(this, string.Format(msg, args));
        }

        private void LoadSettings()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            try
            {
                path = !string.IsNullOrWhiteSpace(path)
                    ? Path.Combine(path, "U-Wii-X Fusion", "ftp.json")
                    : "ftp.json";
                string json = File.ReadAllText(path);
                _settings = _json.Deserialize<FtpSettings>(json);
                if (_settings != null)
                    _settings.Loaded = true;
                else
                    _settings = new FtpSettings { Loaded = false };
            }
            catch
            {
                _settings = new FtpSettings { Loaded = false };
            }
        }

        /// <summary>测试连接并验证 Aurora（SITE REVISION）。</summary>
        public bool TestConnection(string ip, string user, string pass, string port)
        {
            _settings.IpAddress = ip ?? "";
            _settings.Username = user ?? "";
            _settings.Password = pass ?? "";
            _settings.Port = string.IsNullOrWhiteSpace(port) ? "21" : port;
            _settings.Loaded = true;
            try
            {
                if (MakeConnection())
                    return true;
                SendStatus("连接测试失败。");
                return false;
            }
            catch (Exception ex)
            {
                SendStatus("错误: {0}", ex.Message);
                return false;
            }
        }

        private bool MakeConnection()
        {
            _client = new FtpClient
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                Host = _settings.IpAddress,
                SocketKeepAlive = true
            };
            if (!int.TryParse(_settings.Port, out int port))
            {
                port = 21;
                _settings.Port = port.ToString(CultureInfo.InvariantCulture);
            }
            _client.Port = port;
            SendStatus("正在连接 {0}...", _settings.IpAddress);
            _client.Connect();
            if (!_client.IsConnected)
                return false;
            SendStatus("已连接 {0}，正在检查 Aurora...", _settings.IpAddress);
            var reply = _client.Execute("SITE REVISION");
            if (!reply.Success)
                return false;
            SendStatus("已连接 Aurora，版本: {0}", reply.Message);
            return true;
        }

        public void SaveSettings()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = !string.IsNullOrWhiteSpace(path)
                ? Path.Combine(path, "U-Wii-X Fusion", "ftp.json")
                : "ftp.json";
            path = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(dir))
                return;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, _json.Serialize(_settings));
        }

        public void SaveSettings(string ip, string user, string pass, string port)
        {
            _settings.IpAddress = ip ?? "";
            _settings.Username = user ?? "";
            _settings.Password = pass ?? "";
            _settings.Port = string.IsNullOrWhiteSpace(port) ? "21" : port;
            _settings.Loaded = true;
            SaveSettings();
        }

        /// <summary>切换到 /Game/Data/GameData/</summary>
        public bool NavigateToGameDataDir()
        {
            if (_client == null || !_client.IsConnected)
            {
                if (!MakeConnection())
                {
                    SendStatus("无法连接到 {0}", _settings.IpAddress);
                    return false;
                }
            }
            if (_client == null) return false;
            const string dir = "/Game/Data/GameData/";
            SendStatus("切换工作目录到 {0}...", dir);
            _client.SetWorkingDirectory(dir);
            return _client.GetWorkingDirectory().Equals(dir, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>切换到 /Game/Data/GameData/{assetName}/</summary>
        public bool NavigateToAssetDir(string assetName)
        {
            if (!NavigateToGameDataDir()) return false;
            string dir = "/Game/Data/GameData/" + assetName + "/";
            SendStatus("切换工作目录到 {0}...", dir);
            _client.SetWorkingDirectory(dir);
            return _client.GetWorkingDirectory().Equals(dir, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>获取 GameData 下子目录名列表（每个为 Title ID 等）。</summary>
        public string[] GetDirList()
        {
            return _client.GetListing()
                .Where(item => item.Type == FtpFileSystemObjectType.Directory)
                .Select(item => item.Name)
                .ToArray();
        }

        /// <summary>读取指定 asset 目录下的文件内容。</summary>
        public byte[] GetAssetData(string file, string assetDir)
        {
            if (!NavigateToAssetDir(assetDir)) return null;
            int size = _client.GetListing()
                .Where(item => item.Name.Equals(file, StringComparison.InvariantCultureIgnoreCase))
                .Select(item => (int)item.Size)
                .FirstOrDefault();
            if (size <= 0) return null;
            var data = new byte[size];
            int offset = 0;
            using (var stream = _client.OpenRead(file))
            {
                while (offset < data.Length)
                    offset += stream.Read(data, offset, data.Length - offset);
            }
            return data;
        }

        /// <summary>写入数据到指定 asset 目录下的文件。</summary>
        public bool SendAssetData(string file, string assetDir, byte[] data)
        {
            if (!NavigateToAssetDir(assetDir)) return false;
            using (var stream = _client.OpenWrite(file))
                stream.Write(data, 0, data.Length);
            return true;
        }

        /// <summary>从主机 /Game/Data/DataBases/ 下载指定文件到本地路径。</summary>
        /// <returns>是否成功</returns>
        public bool DownloadFileFromDataBases(string fileName, string localFilePath)
        {
            if (_client == null || !_client.IsConnected)
            {
                if (!MakeConnection())
                {
                    SendStatus("无法连接到 {0}", _settings.IpAddress);
                    return false;
                }
            }
            if (_client == null) return false;
            const string dir = "/Game/Data/DataBases/";
            SendStatus("切换工作目录到 {0}...", dir);
            _client.SetWorkingDirectory(dir);
            if (!_client.GetWorkingDirectory().Equals(dir, StringComparison.InvariantCultureIgnoreCase))
                return false;
            int size = _client.GetListing()
                .Where(item => item.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
                .Select(item => (int)item.Size)
                .FirstOrDefault();
            if (size <= 0)
            {
                SendStatus("未找到 {0}。", fileName);
                return false;
            }
            var data = new byte[size];
            int offset = 0;
            using (var stream = _client.OpenRead(fileName))
            {
                while (offset < data.Length)
                    offset += stream.Read(data, offset, data.Length - offset);
            }
            File.WriteAllBytes(localFilePath, data);
            SendStatus("{0} 已保存到 {1}", fileName, localFilePath);
            return true;
        }

        /// <summary>从主机下载 Content.db 到本地路径。</summary>
        public bool DownloadContentDb(string localPath)
        {
            return DownloadFileFromDataBases("Content.db", localPath);
        }

        /// <summary>下载 settings.db 和 Content.db 到指定文件夹。</summary>
        /// <param name="localFolderPath">本地文件夹路径</param>
        /// <param name="contentOk">是否成功下载 Content.db</param>
        /// <param name="settingsOk">是否成功下载 settings.db</param>
        public void DownloadDatabases(string localFolderPath, out bool contentOk, out bool settingsOk)
        {
            contentOk = false;
            settingsOk = false;
            if (string.IsNullOrWhiteSpace(localFolderPath) || !Directory.Exists(localFolderPath))
                return;
            contentOk = DownloadFileFromDataBases("Content.db", Path.Combine(localFolderPath, "Content.db"));
            settingsOk = DownloadFileFromDataBases("settings.db", Path.Combine(localFolderPath, "settings.db"));
        }

        /// <summary>FTP 设置，JSON 键与 Aurora 兼容：ip, user, pass, port</summary>
        private class FtpSettings
        {
            public bool Loaded { get; set; }
            public string IpAddress { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Port { get; set; }
        }
    }
}
