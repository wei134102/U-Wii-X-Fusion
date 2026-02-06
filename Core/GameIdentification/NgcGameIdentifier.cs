using System.IO;
using System.Linq;
using U_Wii_X_Fusion.Core.Interfaces;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    /// <summary>
    /// 任天堂 GameCube (NGC) 游戏识别
    /// </summary>
    public class NgcGameIdentifier : IGameIdentifier
    {
        // .iso 与 Wii 共用，此处仅识别 .gcm；若需 NGC 的 .iso 可再按文件大小区分
        private readonly string[] _supportedExtensions = { ".gcm" };

        public GameInfo IdentifyGame(string filePath)
        {
            if (!IsSupportedFormat(filePath))
                return null;

            var gameInfo = new GameInfo
            {
                Path = filePath,
                Platform = "NGC",
                PlatformType = "NGC",
                Format = Path.GetExtension(filePath).ToLower(),
                Title = Path.GetFileNameWithoutExtension(filePath),
                Size = new FileInfo(filePath).Length,
                Status = "已识别"
            };

            return gameInfo;
        }

        public bool IsSupportedFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return _supportedExtensions.Contains(extension);
        }

        public string GetPlatform()
        {
            return "NGC";
        }
    }
}
