using System;
using System.Collections.Generic;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Database.Interfaces
{
    public interface IGameDatabase
    {
        void Initialize();
        void AddGame(GameInfo game);
        void UpdateGame(GameInfo game);
        void RemoveGame(string gameId);
        GameInfo GetGame(string gameId);
        List<GameInfo> GetAllGames();
        List<GameInfo> GetGamesByPlatform(string platform);
        List<GameInfo> SearchGames(string query);
        List<GameInfo> FilterGames(string genre = null, string language = null, string controller = null, 
            string region = null, string platformType = null, int? players = null);
    }
}
