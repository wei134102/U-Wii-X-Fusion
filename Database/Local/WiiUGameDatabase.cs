using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Interfaces;

namespace U_Wii_X_Fusion.Database.Local
{
    /// <summary>
    /// Wii U 游戏数据库：从 wiiutdb.xml 与 gametitle_wiiu.txt 加载，提供按 GameId 查询。
    /// </summary>
    public class WiiUGameDatabase : IGameDatabase
    {
        private readonly string _databasePath;
        private readonly string _chineseTitlesPath;
        private List<GameInfo> _games;
        private Dictionary<string, string> _chineseTitles;

        public WiiUGameDatabase(string databasePath = "Data\\wiiutdb.xml", string chineseTitlesPath = "Data\\gametitle_wiiu.txt")
        {
            _databasePath = databasePath;
            _chineseTitlesPath = chineseTitlesPath;
            _games = new List<GameInfo>();
            _chineseTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void Initialize()
        {
            try
            {
                LoadChineseTitles();
                ParseGameTdbXml();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiiU database load error: {ex.Message}");
            }
        }

        private void LoadChineseTitles()
        {
            try
            {
                if (!File.Exists(_chineseTitlesPath)) return;
                var lines = File.ReadAllLines(_chineseTitlesPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || !trimmed.Contains("=") || trimmed.StartsWith("TITLES", StringComparison.OrdinalIgnoreCase))
                        continue;
                    int idx = trimmed.IndexOf('=');
                    var gameId = trimmed.Substring(0, idx).Trim();
                    var title = trimmed.Substring(idx + 1).Trim();
                    if (!string.IsNullOrEmpty(gameId))
                        _chineseTitles[gameId] = title;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiiU Chinese titles load error: {ex.Message}");
            }
        }

        private void ParseGameTdbXml()
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    _games = new List<GameInfo>();
                    return;
                }
                var doc = XDocument.Load(_databasePath);
                _games = new List<GameInfo>();
                foreach (var gameElem in doc.Descendants("game"))
                {
                    var game = ParseGameElement(gameElem);
                    if (game != null)
                        _games.Add(game);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiiU XML parse error: {ex.Message}");
                _games = new List<GameInfo>();
            }
        }

        private GameInfo ParseGameElement(XElement gameElem)
        {
            var gameId = gameElem.Element("id")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(gameId)) return null;

            var game = new GameInfo
            {
                GameId = gameId,
                Platform = "Wii U",
                PlatformType = "WiiU",
                ChineseTitle = _chineseTitles.TryGetValue(gameId, out var chineseTitle) ? chineseTitle : string.Empty
            };

            var locales = gameElem.Elements("locale").ToList();
            var enLocale = locales.FirstOrDefault(l => string.Equals(l.Attribute("lang")?.Value, "EN", StringComparison.OrdinalIgnoreCase));
            game.Title = enLocale?.Element("title")?.Value ?? string.Empty;
            game.Synopsis = enLocale?.Element("synopsis")?.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(game.Synopsis))
            {
                string[] preferredLangs = { "ZHCN", "ZHTW", "JA", "ES", "FR", "DE" };
                foreach (var lang in preferredLangs)
                {
                    var loc = locales.FirstOrDefault(l => string.Equals(l.Attribute("lang")?.Value, lang, StringComparison.OrdinalIgnoreCase));
                    var syn = loc?.Element("synopsis")?.Value;
                    if (!string.IsNullOrWhiteSpace(syn))
                    {
                        game.Synopsis = syn;
                        break;
                    }
                }
            }

            game.Region = gameElem.Element("region")?.Value ?? string.Empty;

            var inputElem = gameElem.Element("input");
            if (inputElem != null && int.TryParse(inputElem.Attribute("players")?.Value, out var players))
                game.Players = players;
            else
                game.Players = 1;

            if (inputElem != null)
            {
                foreach (var control in inputElem.Elements("control"))
                {
                    var type = control.Attribute("type")?.Value;
                    if (!string.IsNullOrEmpty(type))
                        game.Controllers.Add(type);
                }
            }

            var genreText = gameElem.Element("genre")?.Value;
            if (!string.IsNullOrEmpty(genreText))
                game.Genres.AddRange(genreText.Split(',').Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)));

            game.Publisher = gameElem.Element("publisher")?.Value ?? string.Empty;
            game.Developer = gameElem.Element("developer")?.Value ?? string.Empty;

            var languagesText = gameElem.Element("languages")?.Value;
            if (!string.IsNullOrEmpty(languagesText))
            {
                foreach (var l in languagesText.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                    if (!game.Languages.Contains(l)) game.Languages.Add(l);
            }
            foreach (var locale in gameElem.Elements("locale"))
            {
                var lang = locale.Attribute("lang")?.Value;
                if (!string.IsNullOrEmpty(lang) && !game.Languages.Contains(lang))
                    game.Languages.Add(lang);
            }

            return game;
        }

        public void AddGame(GameInfo game)
        {
            if (string.IsNullOrEmpty(game.GameId)) return;
            _games.Add(game);
        }

        public void UpdateGame(GameInfo game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.GameId)) return;
            var existing = _games.FirstOrDefault(g => string.Equals(g.GameId, game.GameId, StringComparison.OrdinalIgnoreCase));
            if (existing != null) _games.Remove(existing);
            _games.Add(game);
        }

        public void RemoveGame(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return;
            var g = _games.FirstOrDefault(x => string.Equals(x.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            if (g != null) _games.Remove(g);
        }

        public GameInfo GetGame(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;
            return _games.FirstOrDefault(g => string.Equals(g.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        }

        public List<GameInfo> GetAllGames()
        {
            return _games;
        }

        public List<GameInfo> GetGamesByPlatform(string platform)
        {
            return _games.Where(g => string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public List<GameInfo> SearchGames(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _games;
            var lower = query.ToLower();
            return _games.Where(g =>
                (g.Title != null && g.Title.ToLower().Contains(lower)) ||
                (g.GameId != null && g.GameId.ToLower().Contains(lower)) ||
                (g.ChineseTitle != null && g.ChineseTitle.ToLower().Contains(lower))).ToList();
        }

        public List<GameInfo> FilterGames(string genre = null, string language = null, string controller = null,
            string region = null, string platformType = null, int? players = null)
        {
            var q = _games.AsEnumerable();
            if (!string.IsNullOrEmpty(genre) && genre != "全部游戏类型")
                q = q.Where(g => g.Genres != null && g.Genres.Any(gg => string.Equals(gg, genre, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(language))
                q = q.Where(g => g.Languages != null && g.Languages.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(controller))
                q = q.Where(g => g.Controllers != null && g.Controllers.Any(c => string.Equals(c, controller, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(region))
                q = q.Where(g => string.Equals(g.Region, region, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(platformType))
                q = q.Where(g => string.Equals(g.PlatformType, platformType, StringComparison.OrdinalIgnoreCase));
            if (players.HasValue && players.Value > 0)
                q = q.Where(g => g.Players >= players.Value);
            return q.ToList();
        }

        /// <summary>对指定列表应用与 FilterGames 相同的筛选条件，便于与搜索结果组合使用</summary>
        public List<GameInfo> FilterGameList(IEnumerable<GameInfo> source,
            string genre = null, string language = null, string controller = null,
            string region = null, string platformType = null, int? players = null)
        {
            if (source == null) return new List<GameInfo>();
            var filtered = source.AsEnumerable();
            if (!string.IsNullOrEmpty(genre) && genre != "全部游戏类型")
            {
                var genreLower = genre.ToLower();
                filtered = filtered.Where(g => g.Genres != null && g.Genres.Any(genreItem => genreItem != null && genreItem.ToLower() == genreLower));
            }
            if (!string.IsNullOrEmpty(language) && language != "全部语言")
            {
                if (language == "中文")
                    filtered = filtered.Where(g => g.Languages != null && (g.Languages.Contains("ZH") || g.Languages.Contains("CN")));
                else
                    filtered = filtered.Where(g => g.Languages != null && g.Languages.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)));
            }
            if (!string.IsNullOrEmpty(controller) && controller != "全部控制器")
            {
                var controllerLower = controller.ToLower();
                filtered = filtered.Where(g => g.Controllers != null && g.Controllers.Any(c => c != null && c.ToLower() == controllerLower));
            }
            if (!string.IsNullOrEmpty(region) && region != "全部区域")
                filtered = filtered.Where(g => g.Region != null && g.Region.ToUpper().Contains(region.ToUpper()));
            if (!string.IsNullOrEmpty(platformType) && platformType != "全部平台类型" && platformType != "全部平台")
                filtered = filtered.Where(g => string.Equals(g.PlatformType, platformType, StringComparison.OrdinalIgnoreCase));
            if (players.HasValue && players.Value > 0)
                filtered = filtered.Where(g => g.Players >= players.Value);
            return filtered.ToList();
        }

        /// <summary>
        /// 根据 ProductCode（4 位，如 WAHJ）解析出 wiiutdb/gametitle 中的 6 位 id（如 WAHJ01），用于中文名与封面 ID。
        /// 优先返回 6 位 id，其次 4 位。
        /// </summary>
        public string ResolveGameIdFromProductCode(string productCode)
        {
            if (string.IsNullOrEmpty(productCode) || productCode.Length < 4) return null;
            string pc = productCode.Trim().ToUpperInvariant();
            // 先查 _games（来自 wiiutdb.xml）中 id 以 ProductCode 开头的，优先 6 位
            var fromGames = _games.Where(g => g.GameId != null && g.GameId.StartsWith(pc, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.GameId.Length)
                .ThenBy(g => g.GameId)
                .ToList();
            var sixChar = fromGames.FirstOrDefault(g => g.GameId.Length == 6);
            if (sixChar != null) return sixChar.GameId;
            var fourChar = fromGames.FirstOrDefault(g => g.GameId.Length == 4);
            if (fourChar != null) return fourChar.GameId;
            if (fromGames.Count > 0) return fromGames[0].GameId;
            // 再查 _chineseTitles（gametitle_wiiu.txt）的 key
            var fromTitles = _chineseTitles.Keys.Where(k => k.StartsWith(pc, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k.Length)
                .ThenBy(k => k)
                .ToList();
            var k6 = fromTitles.FirstOrDefault(k => k.Length == 6);
            if (k6 != null) return k6;
            if (fromTitles.Count > 0) return fromTitles[0];
            return null;
        }
    }
}
