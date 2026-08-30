using System;
using System.Collections.Generic;
using System.IO;
using U_Wii_X_Fusion.Core.GameIdentification;

namespace U_Wii_X_Fusion.Core
{
    /// <summary>
    /// 读取 WUP Installer「提取 ID」生成的 install/id_installed.txt（每行 16 位 Title ID）。
    /// </summary>
    public static class IdInstalledTxtReader
    {
        public const string FileName = "id_installed.txt";

        public static string GetInstallFilePath(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                return string.Empty;
            return Path.Combine(installDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), FileName);
        }

        /// <summary>从 id_installed.txt 加载已安装本体 Title ID（统一为 00050000 + 后 8 位）。</summary>
        public static HashSet<string> LoadInstalledBaseTitleIds(string filePath)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return set;

            foreach (string line in File.ReadAllLines(filePath))
            {
                string normalized = NormalizeTitleIdLine(line);
                if (string.IsNullOrEmpty(normalized))
                    continue;
                set.Add(WiiUTitleTmdReader.GetBaseTitleId(normalized));
            }

            return set;
        }

        public static bool IsBaseTitleInstalled(HashSet<string> installedBaseTitleIds, string titleIdHex)
        {
            if (installedBaseTitleIds == null || installedBaseTitleIds.Count == 0 || string.IsNullOrWhiteSpace(titleIdHex))
                return false;

            string upper = titleIdHex.Trim().ToUpperInvariant();
            if (installedBaseTitleIds.Contains(upper))
                return true;

            string baseId = WiiUTitleTmdReader.GetBaseTitleId(upper);
            return installedBaseTitleIds.Contains(baseId);
        }

        private static string NormalizeTitleIdLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            var hex = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '#' || c == ';')
                    break;
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                    hex.Append(char.ToUpperInvariant(c));
            }

            if (hex.Length == 16)
                return hex.ToString();
            if (hex.Length > 16)
                return hex.ToString(0, 16);
            return string.Empty;
        }
    }
}
