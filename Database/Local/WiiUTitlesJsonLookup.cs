using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace U_Wii_X_Fusion.Database.Local
{
    /// <summary>
    /// 从 wiiu_titles.json 按 TitleId（16 位）查询，直接得到 game_id、中文名、英文名。
    /// 用于封面匹配（game_id 即封面 ID）及列表显示。
    /// </summary>
    public class WiiUTitlesJsonLookup
    {
        private readonly Dictionary<string, WiiUTitleEntry> _byTitleId = new Dictionary<string, WiiUTitleEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly string _jsonPath;

        public WiiUTitlesJsonLookup(string jsonPath = "Data\\wiiu_titles.json")
        {
            _jsonPath = jsonPath;
        }

        public void Initialize()
        {
            _byTitleId.Clear();
            try
            {
                if (!File.Exists(_jsonPath)) return;
                string json = File.ReadAllText(_jsonPath);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var list = serializer.Deserialize<List<Dictionary<string, object>>>(json);
                if (list == null) return;
                foreach (var obj in list)
                {
                    string titleId = GetString(obj, "title_id");
                    string gameId = GetString(obj, "game_id");
                    if (string.IsNullOrEmpty(titleId)) continue;
                    _byTitleId[titleId.Trim().ToUpperInvariant()] = new WiiUTitleEntry
                    {
                        TitleId = titleId,
                        GameId = gameId ?? string.Empty,
                        ChineseName = GetString(obj, "chinese_name"),
                        EnglishName = GetString(obj, "english_name")
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiiU titles JSON load error: {ex.Message}");
            }
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.TryGetValue(key, out var o) || o == null) return string.Empty;
            return (o as string) ?? o.ToString().Trim();
        }

        /// <summary>按 TitleId（16 位十六进制）查询，返回 game_id、中文名、英文名；未找到返回 null。</summary>
        public WiiUTitleEntry GetByTitleId(string titleId)
        {
            if (string.IsNullOrEmpty(titleId)) return null;
            var key = titleId.Trim().ToUpperInvariant();
            return _byTitleId.TryGetValue(key, out var e) ? e : null;
        }

        public class WiiUTitleEntry
        {
            public string TitleId { get; set; }
            public string GameId { get; set; }
            public string ChineseName { get; set; }
            public string EnglishName { get; set; }
        }
    }
}
