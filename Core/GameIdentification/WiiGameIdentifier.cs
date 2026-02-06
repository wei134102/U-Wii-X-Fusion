using System.IO;
using System.Linq;
using U_Wii_X_Fusion.Core.Interfaces;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    /// <summary>
    /// Wii 游戏识别：
    /// - 支持 .iso / .wbfs / .wad
    /// - 对 ISO 等原盘格式，会尝试从光盘头中读取真正的 GameID 和标题；
    ///   文件名不是 ID.wbfs 也能正确识别。
    /// </summary>
    public class WiiGameIdentifier : IGameIdentifier
    {
        private readonly string[] _supportedExtensions = { ".iso", ".wbfs", ".wad" };

        public GameInfo IdentifyGame(string filePath)
        {
            if (!IsSupportedFormat(filePath))
                return null;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string titleFromPath = Path.GetFileNameWithoutExtension(filePath);

            string gameId = null;
            string title = titleFromPath;
            bool isWii = true;
            bool isGc = false;
            bool headerOk = false;

            // 对 ISO 这类原盘格式，优先尝试从光盘头读取 GameID/标题
            if (ext == ".iso")
            {
                if (DiscHeaderReader.TryReadDiscHeader(filePath, out var id, out var headerTitle, out var headerIsWii, out var headerIsGc))
                {
                    headerOk = true;
                    gameId = id;
                    isWii = headerIsWii || !headerIsGc;
                    isGc = headerIsGc;
                    if (!string.IsNullOrWhiteSpace(headerTitle))
                        title = headerTitle;
                }
            }
            // 非标准 WBFS：根据你的说明，GameID 位于偏移 0x200 处的 6 字节
            else if (ext == ".wbfs")
            {
                if (DiscHeaderReader.TryReadWbfsGameId(filePath, out var id))
                {
                    headerOk = true;
                    gameId = id;
                    isWii = true;
                    isGc = false;
                }
            }

            var info = new GameInfo
            {
                Path = filePath,
                Platform = isGc ? "NGC" : "Wii",
                PlatformType = isGc ? "NGC" : null,
                Format = ext,
                Title = title,
                GameId = gameId, // 注意：后续扫描逻辑会在 GameId 为空时回退为文件名/目录名
                Size = new FileInfo(filePath).Length,
                Status = headerOk ? "已识别(光盘头)" : "已识别"
            };

            return info;
        }

        public bool IsSupportedFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return _supportedExtensions.Contains(extension);
        }

        public string GetPlatform()
        {
            return "Wii";
        }
    }
}

