using System;
using System.Collections.Generic;
using System.Linq;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Interfaces;

namespace U_Wii_X_Fusion.Database.Local
{
    public class LocalGameDatabase : IGameDatabase
    {
        private readonly Dictionary<string, GameInfo> _games;

        public LocalGameDatabase()
        {
            _games = new Dictionary<string, GameInfo>();
        }

        public void Initialize()
        {
            // 初始化数据库，这里可以从文件加载数据
            // 暂时只初始化内存字典
        }

        public void AddGame(GameInfo game)
        {
            if (string.IsNullOrEmpty(game.GameId))
            {
                game.GameId = GenerateGameId(game);
            }
            _games[game.GameId] = game;
        }

        public void UpdateGame(GameInfo game)
        {
            if (_games.ContainsKey(game.GameId))
            {
                _games[game.GameId] = game;
            }
        }

        public void RemoveGame(string gameId)
        {
            if (_games.ContainsKey(gameId))
            {
                _games.Remove(gameId);
            }
        }

        public GameInfo GetGame(string gameId)
        {
            return _games.TryGetValue(gameId, out var game) ? game : null;
        }

        public List<GameInfo> GetAllGames()
        {
            return _games.Values.ToList();
        }

        public List<GameInfo> GetGamesByPlatform(string platform)
        {
            return _games.Values.Where(g => g.Platform == platform).ToList();
        }

        public List<GameInfo> SearchGames(string query)
        {
            string lowerQuery = query.ToLower();
            return _games.Values.Where(g => g.Title.ToLower().Contains(lowerQuery) || g.GameId.ToLower().Contains(lowerQuery)).ToList();
        }

        public List<GameInfo> FilterGames(string genre = null, string language = null, string controller = null, 
            string region = null, string platformType = null, int? players = null)
        {
            var filteredGames = _games.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(genre) && genre != "全部游戏类型")
            {
                var genreLower = genre.ToLower();
                filteredGames = filteredGames.Where(g => 
                    g.Genres != null && g.Genres.Any(genreItem => genreItem.ToLower() == genreLower)
                );
            }

            if (!string.IsNullOrEmpty(language) && language != "全部语言")
            {
                if (language == "中文")
                {
                    filteredGames = filteredGames.Where(g => 
                        g.Languages != null && (g.Languages.Contains("ZH") || g.Languages.Contains("CN"))
                    );
                }
                else
                {
                    filteredGames = filteredGames.Where(g => 
                        g.Languages != null && g.Languages.Contains(language)
                    );
                }
            }

            if (!string.IsNullOrEmpty(controller) && controller != "全部控制器")
            {
                var controllerLower = controller.ToLower();
                filteredGames = filteredGames.Where(g => 
                    g.Controllers != null && g.Controllers.Any(c => c.ToLower() == controllerLower)
                );
            }

            if (!string.IsNullOrEmpty(region) && region != "全部区域")
            {
                filteredGames = filteredGames.Where(g => 
                    !string.IsNullOrEmpty(g.Region) && g.Region.ToUpper().Contains(region.ToUpper())
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
            return $"{game.Platform}_{game.Title}_{Guid.NewGuid()}".Replace(" ", "_").ToLower();
        }
    }
}
