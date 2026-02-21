using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// INI 读写辅助类。保留 section/key 大小写，UTF-8 读写。
    /// </summary>
    public static class IniHelper
    {
        private static readonly Regex SectionRegex = new Regex(@"^\s*\[\s*(.+?)\s*\]\s*$", RegexOptions.Compiled);
        private static readonly Regex KeyValueRegex = new Regex(@"^\s*([^=]+?)\s*=\s*(.*)$", RegexOptions.Compiled);

        /// <summary>
        /// 读取 INI 文件所有节。返回：节名 -> (键 -> 值)，键保留原始大小写。
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> ReadAllSections(string filePath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return result;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath, new UTF8Encoding(false));
            }
            catch
            {
                try
                {
                    lines = File.ReadAllLines(filePath, Encoding.Default);
                }
                catch
                {
                    return result;
                }
            }

            string currentSection = null;
            Dictionary<string, string> currentDict = null;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                var sectionMatch = SectionRegex.Match(trimmed);
                if (sectionMatch.Success)
                {
                    currentSection = sectionMatch.Groups[1].Value.Trim();
                    currentDict = new Dictionary<string, string>(StringComparer.Ordinal);
                    result[currentSection] = currentDict;
                    continue;
                }

                var kvMatch = KeyValueRegex.Match(trimmed);
                if (kvMatch.Success && currentDict != null)
                {
                    string key = kvMatch.Groups[1].Value.Trim();
                    string value = kvMatch.Groups[2].Value.Trim();
                    if (!string.IsNullOrEmpty(key))
                        currentDict[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定节的所有键值对（保留键大小写）。节不存在返回空字典。
        /// </summary>
        public static Dictionary<string, string> GetSection(string filePath, string sectionName)
        {
            var all = ReadAllSections(filePath);
            if (all.TryGetValue(sectionName, out var dict))
                return new Dictionary<string, string>(dict, StringComparer.Ordinal);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 更新或写入指定节，然后写回文件。保留文件中其他节不变；键保留传入的大小写。
        /// </summary>
        public static void WriteSection(string filePath, string sectionName, Dictionary<string, string> keyValues)
        {
            if (string.IsNullOrEmpty(filePath) || keyValues == null)
                return;

            var all = ReadAllSections(filePath);
            all[sectionName] = new Dictionary<string, string>(keyValues, StringComparer.Ordinal);
            WriteAllSections(filePath, all);
        }

        /// <summary>
        /// 将完整节字典写回 INI 文件（UTF-8 无 BOM）。
        /// </summary>
        public static void WriteAllSections(string filePath, Dictionary<string, Dictionary<string, string>> sections)
        {
            if (string.IsNullOrEmpty(filePath) || sections == null)
                return;

            var sb = new StringBuilder();
            foreach (var section in sections)
            {
                sb.AppendLine("[" + section.Key + "]");
                foreach (var kv in section.Value)
                    sb.AppendLine(kv.Key + "=" + (kv.Value ?? ""));
                sb.AppendLine();
            }

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
