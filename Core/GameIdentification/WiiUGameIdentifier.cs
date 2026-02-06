using System.IO;
using System.Linq;
using U_Wii_X_Fusion.Core.Interfaces;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    public class WiiUGameIdentifier : IGameIdentifier
    {
        private readonly string[] _supportedExtensions = { ".wud", ".rpx", ".wux" };

        public GameInfo IdentifyGame(string filePath)
        {
            if (!IsSupportedFormat(filePath))
                return null;

            var gameInfo = new GameInfo
            {
                Path = filePath,
                Platform = "Wii U",
                Format = Path.GetExtension(filePath).ToLower(),
                Title = Path.GetFileNameWithoutExtension(filePath),
                Size = new FileInfo(filePath).Length,
                Status = "已识别"
            };

            // 这里可以添加更详细的识别逻辑，比如解析游戏头信息等
            // 暂时使用文件名作为游戏标题

            return gameInfo;
        }

        public bool IsSupportedFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return _supportedExtensions.Contains(extension);
        }

        public string GetPlatform()
        {
            return "Wii U";
        }
    }
}
