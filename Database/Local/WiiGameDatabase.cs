using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Interfaces;

namespace U_Wii_X_Fusion.Database.Local
{
    public class WiiGameDatabase : IGameDatabase
    {
        private readonly string _databasePath;
        private readonly string _chineseTitlesPath;
        private List<GameInfo> _games;
        private Dictionary<string, string> _chineseTitles;

        public WiiGameDatabase(string databasePath = "Data\\wiitdb.xml", string chineseTitlesPath = "Data\\gametitle.txt")
        {
            _databasePath = databasePath;
            _chineseTitlesPath = chineseTitlesPath;
            _games = new List<GameInfo>();
            _chineseTitles = new Dictionary<string, string>();
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
                Console.WriteLine($"Error loading Wii database: {ex.Message}");
            }
        }

        private void LoadChineseTitles()
        {
            try
            {
                if (File.Exists(_chineseTitlesPath))
                {
                    var lines = File.ReadAllLines(_chineseTitlesPath);
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine) && trimmedLine.Contains('=') && !trimmedLine.StartsWith("TITLES"))
                        {
                            var parts = trimmedLine.Split('=');
                            if (parts.Length >= 2)
                            {
                                var gameId = parts[0].Trim();
                                var title = parts[1].Trim();
                                _chineseTitles[gameId] = title;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Chinese titles: {ex.Message}");
            }
        }

        private void ParseGameTdbXml()
        {
            try
            {
                var doc = XDocument.Load(_databasePath);
                _games = new List<GameInfo>();

                foreach (var gameElem in doc.Descendants("game"))
                {
                    var game = ParseGameElement(gameElem);
                    if (game != null)
                    {
                        _games.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing GameTDB XML: {ex.Message}");
                throw;
            }
        }

        private GameInfo ParseGameElement(XElement gameElem)
        {
            var gameId = gameElem.Element("id")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(gameId))
                return null;

            var game = new GameInfo
            {
                GameId = gameId,
                ChineseTitle = _chineseTitles.TryGetValue(gameId, out var chineseTitle) ? chineseTitle : string.Empty
            };

            // Determine game type
            if (gameId.StartsWith("R") || gameId.StartsWith("S"))
            {
                game.Platform = "Wii";
            }
            else if (gameId.StartsWith("G") || gameId.StartsWith("H"))
            {
                game.Platform = "NGC";
            }
            else
            {
                game.Platform = "Unknown";
            }

            // Get title from English locale
            var enLocale = gameElem.Descendants("locale").FirstOrDefault(l => l.Attribute("lang")?.Value == "EN");
            game.Title = enLocale?.Element("title")?.Value ?? string.Empty;

            // Get region
            game.Region = gameElem.Element("region")?.Value ?? string.Empty;

            // Get players
            var inputElem = gameElem.Element("input");
            if (inputElem != null && int.TryParse(inputElem.Attribute("players")?.Value, out var players))
            {
                game.Players = players;
            }
            else
            {
                game.Players = 1;
            }

            // Get controllers
            if (inputElem != null)
            {
                foreach (var control in inputElem.Elements("control"))
                {
                    var type = control.Attribute("type")?.Value;
                    if (!string.IsNullOrEmpty(type))
                    {
                        game.Controllers.Add(type);
                    }
                }
            }

            // Get genres
            var genreText = gameElem.Element("genre")?.Value;
            if (!string.IsNullOrEmpty(genreText))
            {
                game.Genres.AddRange(genreText.Split(',').Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)));
            }

            // Get publisher and developer
            game.Publisher = gameElem.Element("publisher")?.Value ?? string.Empty;
            game.Developer = gameElem.Element("developer")?.Value ?? string.Empty;

            // Get languages
            var languagesSet = new HashSet<string>();
            var languagesText = gameElem.Element("languages")?.Value;
            if (!string.IsNullOrEmpty(languagesText))
            {
                languagesSet.UnionWith(languagesText.Split(',').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
            }
            foreach (var locale in gameElem.Elements("locale"))
            {
                var lang = locale.Attribute("lang")?.Value;
                if (!string.IsNullOrEmpty(lang))
                {
                    languagesSet.Add(lang);
                }
            }
            game.Languages = languagesSet.ToList();

            // Get platform type
            var typeElem = gameElem.Element("type");
            var platformType = typeElem?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(platformType))
            {
                if (game.Platform == "Wii")
                    platformType = "Wii";
                else if (game.Platform == "NGC")
                    platformType = "NGC";
            }
            game.PlatformType = platformType;

            return game;
        }

        public void AddGame(GameInfo game)
        {
            if (string.IsNullOrEmpty(game.GameId))
            {
                game.GameId = GenerateGameId(game);
            }
            _games.Add(game);
        }

        public void UpdateGame(GameInfo game)
        {
            var existingGame = _games.FirstOrDefault(g => g.GameId == game.GameId);
            if (existingGame != null)
            {
                _games.Remove(existingGame);
                _games.Add(game);
            }
        }

        public void RemoveGame(string gameId)
        {
            var game = _games.FirstOrDefault(g => g.GameId == gameId);
            if (game != null)
            {
                _games.Remove(game);
            }
        }

        public GameInfo GetGame(string gameId)
        {
            return _games.FirstOrDefault(g => g.GameId == gameId);
        }

        public List<GameInfo> GetAllGames()
        {
            return _games;
        }

        public List<GameInfo> GetGamesByPlatform(string platform)
        {
            return _games.Where(g => g.Platform == platform).ToList();
        }

        public List<GameInfo> SearchGames(string query)
        {
            string lowerQuery = query.ToLower();
            return _games.Where(g => 
                g.Title.ToLower().Contains(lowerQuery) || 
                g.GameId.ToLower().Contains(lowerQuery) ||
                g.ChineseTitle.ToLower().Contains(lowerQuery)
            ).ToList();
        }

        public List<GameInfo> FilterGames(string genre = null, string language = null, string controller = null, 
            string region = null, string platformType = null, int? players = null)
        {
            var filteredGames = _games.AsEnumerable();

            if (!string.IsNullOrEmpty(genre) && genre != "全部游戏类型")
            {
                var genreLower = genre.ToLower();
                filteredGames = filteredGames.Where(g => 
                    g.Genres.Any(genreItem => genreItem.ToLower() == genreLower)
                );
            }

            if (!string.IsNullOrEmpty(language) && language != "全部语言")
            {
                if (language == "中文")
                {
                    filteredGames = filteredGames.Where(g => 
                        g.Languages.Contains("ZH") || g.Languages.Contains("CN")
                    );
                }
                else
                {
                    filteredGames = filteredGames.Where(g => 
                        g.Languages.Contains(language)
                    );
                }
            }

            if (!string.IsNullOrEmpty(controller) && controller != "全部控制器")
            {
                var controllerLower = controller.ToLower();
                filteredGames = filteredGames.Where(g => 
                    g.Controllers.Any(c => c.ToLower() == controllerLower)
                );
            }

            if (!string.IsNullOrEmpty(region) && region != "全部区域")
            {
                filteredGames = filteredGames.Where(g => 
                    g.Region.ToUpper().Contains(region.ToUpper())
                );
            }

            if (!string.IsNullOrEmpty(platformType) && platformType != "全部平台类型")
            {
                filteredGames = filteredGames.Where(g => 
                    g.PlatformType == platformType
                );
            }

            if (players.HasValue && players > 0)
            {
                filteredGames = filteredGames.Where(g => 
                    g.Players >= players.Value
                );
            }

            return filteredGames.ToList();
        }

        private string GenerateGameId(GameInfo game)
        {
            return $"wii_{game.Title.Replace(' ', '_').ToLower()}_{Guid.NewGuid()}".Replace(" ", "_").ToLower();
        }
    }
}
