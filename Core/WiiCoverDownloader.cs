using System;
using System.IO;
using System.Net;
using U_Wii_X_Fusion.Core.Settings;

namespace U_Wii_X_Fusion.Core
{
    /// <summary>从 GameTDB 下载 Wii / Wii U 封面。</summary>
    public static class WiiCoverDownloader
    {
        private const string WiiBaseUrl = "https://art.gametdb.com/wii";
        private const string WiiUBaseUrl = "https://art.gametdb.com/wiiu";

        /// <summary>
        /// 为 Wii 游戏下载 2D / 3D / Disc / Full 封面，保存到 coverPath\对应子目录。
        /// 返回是否至少有一种封面成功下载或已存在。
        /// </summary>
        public static bool DownloadWiiCovers(string gameId, string coverBasePath)
        {
            if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(coverBasePath))
                return false;

            gameId = gameId.Trim().ToUpperInvariant();
            string region = GetWiiTdbRegion(gameId);
            bool anyOk = false;

            try
            {
                var s = SettingsManager.GetSettings();
                bool d2d = s?.DownloadWiiCover2D ?? true;
                bool d3d = s?.DownloadWiiCover3D ?? true;
                bool dDisc = s?.DownloadWiiDiscCover ?? true;
                bool dFull = s?.DownloadWiiFullCover ?? true;

                // 2D 封面（cover）
                if (d2d)
                {
                    string dir2d = Path.Combine(coverBasePath, "2D");
                    string path2d = Path.Combine(dir2d, gameId + ".png");
                    string url2d = string.Format("{0}/cover/{1}/{2}.png", WiiBaseUrl, region, gameId);
                    if (DownloadIfMissing(url2d, path2d))
                        anyOk = true;
                }

                // 3D 封面
                if (d3d)
                {
                    string cover3dDir = Path.Combine(coverBasePath, "3d");
                    string cover3dPath = Path.Combine(cover3dDir, gameId + ".png");
                    string cover3dUrl = string.Format("{0}/cover3D/{1}/{2}.png", WiiBaseUrl, region, gameId);
                    if (DownloadIfMissing(cover3dUrl, cover3dPath))
                        anyOk = true;
                }

                // Disc 封面
                if (dDisc)
                {
                    string discDir = Path.Combine(coverBasePath, "disc");
                    string discPath = Path.Combine(discDir, gameId + ".png");
                    string discUrl = string.Format("{0}/disc/{1}/{2}.png", WiiBaseUrl, region, gameId);
                    if (DownloadIfMissing(discUrl, discPath))
                        anyOk = true;
                }

                // Full 封面（coverfull）
                if (dFull)
                {
                    string fullDir = Path.Combine(coverBasePath, "full");
                    string fullPath = Path.Combine(fullDir, gameId + ".png");
                    string fullUrl = string.Format("{0}/coverfull/{1}/{2}.png", WiiBaseUrl, region, gameId);
                    if (DownloadIfMissing(fullUrl, fullPath))
                        anyOk = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Wii cover download error: " + ex.Message);
            }

            return anyOk;
        }

        /// <summary>
        /// 为 Wii U 游戏下载 Disc + 3D 封面，保存到 coverPath\disc 和 coverPath\3d。
        /// 返回是否至少有一种封面成功下载或已存在。
        /// </summary>
        public static bool DownloadWiiUCovers(string titleId, string coverBasePath)
        {
            if (string.IsNullOrWhiteSpace(titleId) || string.IsNullOrWhiteSpace(coverBasePath))
                return false;

            titleId = titleId.Trim().ToUpperInvariant();
            string region = GetWiiTdbRegion(titleId);
            bool anyOk = false;

            try
            {
                var s = SettingsManager.GetSettings();
                bool d3d = s?.DownloadWiiCover3D ?? true;
                bool dDisc = s?.DownloadWiiDiscCover ?? true;

                if (d3d)
                {
                    string cover3dDir = Path.Combine(coverBasePath, "3d");
                    string cover3dPath = Path.Combine(cover3dDir, titleId + ".png");
                    string cover3dUrl = string.Format("{0}/cover3D/{1}/{2}.png", WiiUBaseUrl, region, titleId);
                    if (DownloadIfMissing(cover3dUrl, cover3dPath))
                        anyOk = true;
                }

                if (dDisc)
                {
                    string discDir = Path.Combine(coverBasePath, "disc");
                    string discPath = Path.Combine(discDir, titleId + ".png");
                    string discUrl = string.Format("{0}/disc/{1}/{2}.png", WiiUBaseUrl, region, titleId);
                    if (DownloadIfMissing(discUrl, discPath))
                        anyOk = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Wii U cover download error: " + ex.Message);
            }

            return anyOk;
        }

        /// <summary>根据游戏 ID 第 4 位映射到 GameTDB 所需的区域代码。</summary>
        private static string GetWiiTdbRegion(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 4)
                return "EN";
            char c = char.ToUpperInvariant(id[3]);
            switch (c)
            {
                case 'E':
                case 'N':
                    return "US";
                case 'J':
                    return "JA";
                case 'K':
                case 'Q':
                case 'T':
                    return "KO";
                case 'R':
                    return "RU";
                case 'W':
                    return "ZH";
                default:
                    return "EN";
            }
        }

        /// <summary>如果目标文件不存在，则从指定 URL 下载到该路径。下载成功或者已存在返回 true。</summary>
        private static bool DownloadIfMissing(string url, string path)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(path))
                return false;

            if (File.Exists(path))
                return true;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "U-Wii-X-Fusion/1.0";
                try
                {
                    client.DownloadFile(url, path);
                    return File.Exists(path);
                }
                catch (WebException wex)
                {
                    System.Diagnostics.Debug.WriteLine("Cover download WebException: " + wex.Message);
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Cover download error: " + ex.Message);
                    return false;
                }
            }
        }
    }
}

