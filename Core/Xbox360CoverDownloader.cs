using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;

namespace U_Wii_X_Fusion.Core
{
    /// <summary>
    /// 从 XboxUnity 下载 Xbox 360 游戏封面。
    /// API: CoverInfo.php?titleid=xxx → Cover.php?size=large&cid=xxx
    /// </summary>
    public static class Xbox360CoverDownloader
    {
        private const string BaseUrl = "https://www.xboxunity.net/Resources/Lib";

        /// <summary>下载封面并保存到指定路径，返回是否成功。</summary>
        public static bool DownloadCover(string titleId, string savePath)
        {
            if (string.IsNullOrWhiteSpace(titleId)) return false;
            titleId = titleId.Trim().ToUpperInvariant();
            try
            {
                string coverId = GetCoverId(titleId);
                if (string.IsNullOrEmpty(coverId)) return false;

                byte[] bytes = DownloadCoverImage(coverId);
                if (bytes == null || bytes.Length == 0) return false;

                string dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(savePath, bytes);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Xbox360 cover download error: {ex.Message}");
                return false;
            }
        }

        private static string GetCoverId(string titleId)
        {
            string url = $"{BaseUrl}/CoverInfo.php?titleid={Uri.EscapeDataString(titleId)}";
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "U-Wii-X-Fusion/1.0";
                string json = client.DownloadString(url);
                var serializer = new JavaScriptSerializer();
                var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("Covers", out var coversObj)) return null;
                var covers = coversObj as object[];
                if (covers == null || covers.Length == 0) return null;
                foreach (var c in covers)
                {
                    var cover = c as Dictionary<string, object>;
                    if (cover == null) continue;
                    if (cover.TryGetValue("Official", out var off) && "1".Equals(off?.ToString()))
                    {
                        if (cover.TryGetValue("CoverID", out var cid)) return cid?.ToString();
                    }
                }
                var first = covers[0] as Dictionary<string, object>;
                if (first != null && first.TryGetValue("CoverID", out var id)) return id?.ToString();
                return null;
            }
        }

        private static byte[] DownloadCoverImage(string coverId)
        {
            string url = $"{BaseUrl}/Cover.php?size=large&cid={Uri.EscapeDataString(coverId)}";
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "U-Wii-X-Fusion/1.0";
                return client.DownloadData(url);
            }
        }

    }
}
