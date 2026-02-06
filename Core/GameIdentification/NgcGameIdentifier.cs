using System.IO;
using System.Linq;
using U_Wii_X_Fusion.Core.Interfaces;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    /// <summary>
    /// 任天堂 GameCube (NGC) 游戏识别。
    /// 目前主要用于 .gcm 文件，并尝试通过光盘头读取真正的 GameID 和标题。
    /// </summary>
    public class NgcGameIdentifier : IGameIdentifier
    {
        // .iso 与 Wii 共用，此处仅识别 .gcm；若需 NGC 的 .iso 可再通过 WiiGameIdentifier 内的光盘头判断。
        private readonly string[] _supportedExtensions = { ".gcm" };

        public GameInfo IdentifyGame(string filePath)
        {
            if (!IsSupportedFormat(filePath))
                return null;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string titleFromPath = Path.GetFileNameWithoutExtension(filePath);

            string gameId = null;
            string title = titleFromPath;
            bool isWii;
            bool isGc;
            bool headerOk = DiscHeaderReader.TryReadDiscHeader(filePath, out gameId, out title, out isWii, out isGc);

            var gameInfo = new GameInfo
            {
                Path = filePath,
                Platform = "NGC",
                PlatformType = "NGC",
                Format = ext,
                Title = string.IsNullOrWhiteSpace(title) ? titleFromPath : title,
                GameId = gameId,
                Size = new FileInfo(filePath).Length,
                Status = headerOk ? "已识别(光盘头)" : "已识别"
            };

            return gameInfo;
        }

        public bool IsSupportedFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return _supportedExtensions.Contains(extension);
        }

        public string GetPlatform()
        {
            return "NGC";
        }
    }
}

