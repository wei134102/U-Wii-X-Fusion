using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace U_Wii_X_Fusion.Database.Local
{
    /// <summary>
    /// 从 wiiu_games.json 按 TitleId（16 位十六进制）查询，得到 ProductCode 等信息。
    /// 用于 title.tmd 提取的 TitleId → ProductCode → wiiutdb/gametitle 的 6 位 id（如 WAHJ01）→ 中文名与封面。
    /// </summary>
    public class WiiUGamesJsonLookup
    {
        private readonly Dictionary<string, WiiUGamesEntry> _byTitleId = new Dictionary<string, WiiUGamesEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly string _jsonPath;

        public WiiUGamesJsonLookup(string jsonPath = "Data\\wiiu_games.json")
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
                    string titleId = GetString(obj, "TitleId");
                    string productCode = GetString(obj, "ProductCode");
                    if (string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(productCode)) continue;
                    _byTitleId[titleId.Trim().ToUpperInvariant()] = new WiiUGamesEntry
                    {
                        TitleId = titleId,
                        ProductCode = productCode.Trim().ToUpperInvariant(),
                        Region = GetString(obj, "Region"),
                        Name = GetString(obj, "Name")
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiiU games JSON load error: {ex.Message}");
            }
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.TryGetValue(key, out var o) || o == null) return string.Empty;
            return o.ToString().Trim();
        }

        /// <summary>按 TitleId（16 位十六进制，如 0005000010100D00）查询，返回 ProductCode 等；未找到返回 null。</summary>
        public WiiUGamesEntry GetByTitleId(string titleId)
        {
            if (string.IsNullOrEmpty(titleId)) return null;
            var key = titleId.Trim().ToUpperInvariant();
            return _byTitleId.TryGetValue(key, out var e) ? e : null;
        }

        public class WiiUGamesEntry
        {
            public string TitleId { get; set; }
            public string ProductCode { get; set; }
            public string Region { get; set; }
            public string Name { get; set; }
        }
    }
}
