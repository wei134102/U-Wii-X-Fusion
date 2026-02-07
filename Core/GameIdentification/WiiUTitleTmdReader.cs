using System;
using System.IO;
using System.Text;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    /// <summary>
    /// 从 Wii U 游戏文件夹内的 title.tmd 读取 Title ID。
    /// Wii U 标准格式（WiiUBrew）：偏移 0x18C 起 8 字节为 Title ID（如 0005000C101AC700）。
    /// 50000=游戏本体，5000C=DLC，5000E=升级文件；同一游戏的本体/DLC/升级合并统计大小。
    /// </summary>
    public static class WiiUTitleTmdReader
    {
        /// <summary>Wii U TMD 标准：Title ID 在 0x18C（见 WiiUBrew Title_metadata）</summary>
        public const int TitleIdOffsetWiiU = 0x18C;
        /// <summary>部分导出/旧格式可能使用 0x18，作为回退</summary>
        public const int TitleIdOffsetFallback = 0x18;
        public const int TitleIdLength = 8;

        /// <summary>内容类型：本体 / DLC / 更新</summary>
        public enum WiiUContentType
        {
            Unknown = 0,
            /// <summary>00050000 - 游戏本体</summary>
            Base = 0x00,
            /// <summary>0005000C - DLC</summary>
            Dlc = 0x0C,
            /// <summary>0005000E - 升级文件</summary>
            Update = 0x0E
        }

        /// <summary>
        /// 从 title.tmd 文件路径读取 8 字节 Title ID，转为 16 位十六进制字符串（如 0005000C101AC700）。
        /// 优先使用 Wii U 标准偏移 0x18C；若结果不像 Title ID（非 0005 开头）或文件较小则尝试 0x18。
        /// </summary>
        public static bool TryReadTitleId(string titleTmdPath, out string titleIdHex)
        {
            titleIdHex = null;
            if (string.IsNullOrEmpty(titleTmdPath) || !File.Exists(titleTmdPath))
                return false;
            try
            {
                long fileLen = new FileInfo(titleTmdPath).Length;
                int[] offsets = fileLen >= TitleIdOffsetWiiU + TitleIdLength
                    ? new[] { TitleIdOffsetWiiU, TitleIdOffsetFallback }
                    : new[] { TitleIdOffsetFallback };
                using (var fs = new FileStream(titleTmdPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    foreach (int offset in offsets)
                    {
                        if (fileLen < offset + TitleIdLength) continue;
                        fs.Seek(offset, SeekOrigin.Begin);
                        var buf = new byte[TitleIdLength];
                        if (fs.Read(buf, 0, TitleIdLength) < TitleIdLength) continue;
                        var sb = new StringBuilder(TitleIdLength * 2);
                        foreach (byte b in buf)
                            sb.Append(b.ToString("X2"));
                        string candidate = sb.ToString();
                        if (!LooksLikeTitleId(candidate)) continue;
                        titleIdHex = candidate;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool LooksLikeTitleId(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 16) return false;
            return hex.StartsWith("0005", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>从 16 位十六进制 Title ID 解析内容类型（本体/DLC/更新）。</summary>
        public static WiiUContentType GetContentType(string titleIdHex)
        {
            if (string.IsNullOrEmpty(titleIdHex) || titleIdHex.Length < 8)
                return WiiUContentType.Unknown;
            // 第 4 字节（第 7-8 位十六进制）: 00=本体, 0C=DLC, 0E=更新
            string byte4Hex = titleIdHex.Length >= 8 ? titleIdHex.Substring(6, 2) : "00";
            if (!byte.TryParse(byte4Hex, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                return WiiUContentType.Unknown;
            if (b == 0x00) return WiiUContentType.Base;
            if (b == 0x0C) return WiiUContentType.Dlc;
            if (b == 0x0E) return WiiUContentType.Update;
            return WiiUContentType.Unknown;
        }

        /// <summary>获取“同一游戏”分组键：后 8 位十六进制（4 字节），如 101AC700。本体/DLC/更新共享此键。</summary>
        public static string GetGameUniqueSuffix(string titleIdHex)
        {
            if (string.IsNullOrEmpty(titleIdHex) || titleIdHex.Length < 16)
                return titleIdHex ?? string.Empty;
            return titleIdHex.Substring(8, 8);
        }

        /// <summary>得到游戏本体用的 Title ID（00050000 + 后 8 位），用于显示和数据库匹配。</summary>
        public static string GetBaseTitleId(string titleIdHex)
        {
            string suffix = GetGameUniqueSuffix(titleIdHex);
            if (suffix.Length != 8) return titleIdHex;
            return "00050000" + suffix;
        }

        public static string GetContentTypeDescription(WiiUContentType type)
        {
            switch (type)
            {
                case WiiUContentType.Base: return "本体";
                case WiiUContentType.Dlc: return "DLC";
                case WiiUContentType.Update: return "更新";
                default: return "未知";
            }
        }
    }
}
