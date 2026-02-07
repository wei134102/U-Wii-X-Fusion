using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Database.Local
{
    /// <summary>
    /// Xbox 360 游戏数据库：统一从 Data/xbox360_titles.json 读取与保存。
    /// 格式：Platform, Title, Title ID, Developer, Publisher, Folder Title, Title_cn, Category, Year
    /// </summary>
    public class Xbox360TitlesDatabase
    {
        private readonly string _jsonPath;
        private List<Dictionary<string, object>> _entries = new List<Dictionary<string, object>>();

        public Xbox360TitlesDatabase(string jsonPath = "Data\\xbox360_titles.json")
        {
            _jsonPath = jsonPath;
        }

        public void Initialize()
        {
            _entries = new List<Dictionary<string, object>>();
            try
            {
                string path = ResolvePath(_jsonPath);
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path, Encoding.UTF8);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 500 };
                var list = serializer.Deserialize<List<Dictionary<string, object>>>(json);
                if (list != null)
                    _entries = list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Xbox360 titles load error: {ex.Message}");
            }
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            return path;
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.TryGetValue(key, out var o) || o == null) return string.Empty;
            return (o as string) ?? o.ToString().Trim();
        }

        /// <summary>从条目中获取 Title ID，尝试多种可能的键名（含不可见字符兼容）</summary>
        private static string GetTitleIdFromEntry(Dictionary<string, object> obj)
        {
            if (obj == null) return null;
            foreach (var key in new[] { "Title ID", "title_id", "TitleID", "titleId" })
            {
                var v = GetString(obj, key);
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim().ToUpperInvariant();
            }
            foreach (var kv in obj)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                string n = NormalizeKey(kv.Key);
                if (n == "titleid" || n == "title_id")
                {
                    var v = kv.Value as string ?? kv.Value?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim().ToUpperInvariant();
                }
            }
            return null;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            return new string(key.Where(c => !char.IsWhiteSpace(c) && c != '\u00A0').ToArray()).ToLowerInvariant().Replace("_", "");
        }

        public List<GameInfo> GetAllGames()
        {
            return _entries
                .Select(obj => ParseToGameInfo(obj))
                .Where(g => g != null && !string.IsNullOrEmpty(g.GameId))
                .OrderBy(g => g.GameId)
                .ToList();
        }

        private static GameInfo ParseToGameInfo(Dictionary<string, object> obj)
        {
            string titleId = GetTitleIdFromEntry(obj);
            if (string.IsNullOrWhiteSpace(titleId)) return null;
            string yearStr = GetString(obj, "Year");
            DateTime? releaseDate = null;
            if (!string.IsNullOrWhiteSpace(yearStr) && int.TryParse(yearStr.Trim(), out int y) && y >= 1900 && y <= 2100)
                releaseDate = new DateTime(y, 1, 1);
            return new GameInfo
            {
                GameId = titleId,
                Title = GetString(obj, "Title") ?? GetString(obj, "name"),
                ChineseTitle = GetString(obj, "Title_cn") ?? GetString(obj, "chinese_name"),
                Developer = GetString(obj, "Developer"),
                Publisher = GetString(obj, "Publisher"),
                ReleaseDate = releaseDate,
                Platform = "Xbox 360",
                PlatformType = "Xbox 360",
                Genres = string.IsNullOrWhiteSpace(GetString(obj, "Category")) ? new List<string>() : new List<string> { GetString(obj, "Category") }
            };
        }

        /// <summary>获取所有不重复的分类列表（用于下拉筛选）。</summary>
        public List<string> GetCategories()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in _entries)
            {
                string cat = GetString(obj, "Category");
                if (!string.IsNullOrWhiteSpace(cat))
                    set.Add(cat.Trim());
            }
            return set.OrderBy(x => x).ToList();
        }

        public List<GameInfo> SearchGames(string query)
        {
            var all = GetAllGames();
            if (string.IsNullOrWhiteSpace(query)) return all;
            var q = query.ToLower();
            return all
                .Where(g => (g.GameId != null && g.GameId.ToLower().Contains(q)) ||
                            (g.Title != null && g.Title.ToLower().Contains(q)) ||
                            (g.ChineseTitle != null && g.ChineseTitle.ToLower().Contains(q)) ||
                            (g.Category != null && g.Category.ToLower().Contains(q)) ||
                            (g.Developer != null && g.Developer.ToLower().Contains(q)) ||
                            (g.Publisher != null && g.Publisher.ToLower().Contains(q)) ||
                            (g.Synopsis != null && g.Synopsis.ToLower().Contains(q)))
                .ToList();
        }

        public Xbox360TitleEntry GetByTitleId(string titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId)) return null;
            string id = titleId.Trim().ToUpperInvariant();
            var obj = _entries.FirstOrDefault(e => string.Equals(GetTitleIdFromEntry(e), id, StringComparison.OrdinalIgnoreCase));
            if (obj == null) return null;
            return new Xbox360TitleEntry
            {
                TitleId = GetTitleIdFromEntry(obj),
                Name = GetString(obj, "Title") ?? GetString(obj, "name"),
                ChineseName = GetString(obj, "Title_cn") ?? GetString(obj, "chinese_name")
            };
        }

        /// <summary>添加或更新一条记录，并保存到 JSON（保持原有格式）。</summary>
        public void AddOrUpdate(string titleId, string name, string chineseName)
        {
            if (string.IsNullOrWhiteSpace(titleId)) return;
            string id = titleId.Trim().ToUpperInvariant();
            var existing = _entries.FirstOrDefault(e => string.Equals(GetTitleIdFromEntry(e), id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing["Title"] = name ?? string.Empty;
                existing["Title_cn"] = chineseName ?? string.Empty;
                if (existing.ContainsKey("name")) existing["name"] = name ?? string.Empty;
                if (existing.ContainsKey("chinese_name")) existing["chinese_name"] = chineseName ?? string.Empty;
                if (existing.ContainsKey("Folder Title") && string.IsNullOrEmpty((string)existing["Folder Title"]))
                    existing["Folder Title"] = name ?? string.Empty;
            }
            else
            {
                _entries.Add(new Dictionary<string, object>
                {
                    { "Platform", "Xbox 360" },
                    { "Title", name ?? string.Empty },
                    { "Title ID", id },
                    { "Developer", "" },
                    { "Publisher", "" },
                    { "Folder Title", name ?? string.Empty },
                    { "Title_cn", chineseName ?? string.Empty },
                    { "Category", "" },
                    { "Year", "" }
                });
            }
            Save();
        }

        public void Save()
        {
            try
            {
                string path = ResolvePath(_jsonPath);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                File.WriteAllText(path, serializer.Serialize(_entries));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Xbox360 titles save error: {ex.Message}");
            }
        }

        public class Xbox360TitleEntry
        {
            public string TitleId { get; set; }
            public string Name { get; set; }
            public string ChineseName { get; set; }
        }
    }
}
