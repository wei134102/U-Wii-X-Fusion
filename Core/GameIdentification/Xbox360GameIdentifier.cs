using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using U_Wii_X_Fusion.Core.Interfaces;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    /// <summary>
    /// Xbox 360 游戏识别：支持 ISO、XEX、GOD 格式。
    /// - ISO: 单文件镜像
    /// - XEX: 文件夹内含 default.xex
    /// - GOD: 文件夹名为 8 位十六进制 Title ID，或含 Content/$C 等结构
    /// </summary>
    public class Xbox360GameIdentifier : IGameIdentifier
    {
        private static readonly Regex TitleIdRegex = new Regex(@"^[0-9A-Fa-f]{8}$", RegexOptions.Compiled);
        private readonly string[] _supportedFileExtensions = { ".iso", ".xex" };

        public GameInfo IdentifyGame(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (File.Exists(path))
                return IdentifyFromFile(path);

            if (Directory.Exists(path))
                return IdentifyFromFolder(path);

            return null;
        }

        public bool IsSupportedFormat(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (File.Exists(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                return _supportedFileExtensions.Contains(ext);
            }
            if (Directory.Exists(path))
                return IsXbox360GameFolder(path);
            return false;
        }

        public string GetPlatform() => "Xbox 360";

        /// <summary>判断是否为 Xbox 360 游戏文件夹（GOD 或 XEX 格式）</summary>
        public static bool IsXbox360GameFolder(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return false;
            string name = Path.GetFileName(dirPath)?.Trim() ?? "";
            // GOD: 文件夹名为 8 位十六进制 Title ID
            if (TitleIdRegex.IsMatch(name)) return true;
            // XEX: 内含 default.xex
            if (File.Exists(Path.Combine(dirPath, "default.xex"))) return true;
            // GOD: 内含 $C 或 $T（GOD 容器）或 Content 结构
            if (File.Exists(Path.Combine(dirPath, "$C"))) return true;
            if (File.Exists(Path.Combine(dirPath, "$T"))) return true;
            var contentDir = Path.Combine(dirPath, "Content");
            if (Directory.Exists(contentDir)) return true;
            // GOD: 子目录名为 8 位 hex (Content/0...0/TitleID)
            try
            {
                foreach (var sub in Directory.GetDirectories(dirPath))
                {
                    if (TitleIdRegex.IsMatch(Path.GetFileName(sub) ?? "")) return true;
                }
            }
            catch { }
            return false;
        }

        private GameInfo IdentifyFromFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string gameId = null;
            long size = new FileInfo(filePath).Length;

            if (ext == ".xex")
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "default.xex")))
                {
                    gameId = TryGetTitleIdFromXex(Path.Combine(dir, "default.xex"));
                    if (string.IsNullOrEmpty(gameId))
                        gameId = ExtractTitleIdFromPath(dir);
                }
                else
                {
                    gameId = TryGetTitleIdFromXex(filePath);
                }
                if (string.IsNullOrEmpty(gameId)) gameId = ExtractTitleIdFromPath(filePath);
            }
            else if (ext == ".iso")
            {
                gameId = ExtractTitleIdFromPath(filePath);
            }

            if (string.IsNullOrEmpty(gameId)) gameId = fileName;

            return new GameInfo
            {
                GameId = gameId?.ToUpperInvariant() ?? fileName,
                Path = filePath,
                Platform = "Xbox 360",
                Format = ext,
                Title = fileName,
                Size = size,
                Status = "已识别"
            };
        }

        private GameInfo IdentifyFromFolder(string dirPath)
        {
            string folderName = Path.GetFileName(dirPath)?.Trim() ?? "";
            string gameId = null;
            long size = 0;

            if (TitleIdRegex.IsMatch(folderName))
            {
                gameId = folderName.ToUpperInvariant();
                size = GetDirectorySize(dirPath);
            }
            else if (File.Exists(Path.Combine(dirPath, "default.xex")))
            {
                string xexPath = Path.Combine(dirPath, "default.xex");
                gameId = TryGetTitleIdFromXex(xexPath);
                if (string.IsNullOrEmpty(gameId)) gameId = ExtractTitleIdFromPath(dirPath);
                size = GetDirectorySize(dirPath);
            }
            else
            {
                gameId = FindTitleIdInFolder(dirPath) ?? ExtractTitleIdFromPath(dirPath);
                size = GetDirectorySize(dirPath);
            }

            if (string.IsNullOrEmpty(gameId)) gameId = folderName;

            return new GameInfo
            {
                GameId = gameId.ToUpperInvariant(),
                Path = dirPath,
                Platform = "Xbox 360",
                Format = "GOD",
                Title = folderName,
                Size = size,
                Status = "已识别"
            };
        }

        /// <summary>在文件夹内递归查找 Title ID（8 位 hex 的目录名）</summary>
        private static string FindTitleIdInFolder(string dirPath)
        {
            try
            {
                foreach (var sub in Directory.GetDirectories(dirPath))
                {
                    string name = Path.GetFileName(sub) ?? "";
                    if (TitleIdRegex.IsMatch(name)) return name.ToUpperInvariant();
                    string inner = FindTitleIdInFolder(sub);
                    if (!string.IsNullOrEmpty(inner)) return inner;
                }
            }
            catch { }
            return null;
        }

        /// <summary>从路径中提取可能的 Title ID（8 位十六进制）</summary>
        private static string ExtractTitleIdFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var part in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (TitleIdRegex.IsMatch(part)) return part.ToUpperInvariant();
            }
            return null;
        }

        /// <summary>尝试从 XEX 文件读取 Title ID（可选头 0x407FF Alternate Title IDs）</summary>
        private static string TryGetTitleIdFromXex(string xexPath)
        {
            try
            {
                using (var fs = new FileStream(xexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 0x100) return null;
                    var buf = new byte[256];
                    fs.Read(buf, 0, Math.Min(buf.Length, (int)fs.Length));
                    if (Encoding.ASCII.GetString(buf, 0, 4) != "XEX2") return null;
                    int optionalHeaderCount = BitConverter.ToInt32(buf, 0x14);
                    int offset = 0x18;
                    for (int i = 0; i < optionalHeaderCount && offset + 8 <= buf.Length; i++)
                    {
                        int key = BitConverter.ToInt32(buf, offset);
                        int size = BitConverter.ToInt32(buf, offset + 4);
                        if (key == 0x407FF && size >= 0x0C)
                        {
                            int value = BitConverter.ToInt32(buf, offset + 8);
                            return (value & 0xFFFFFFFF).ToString("X8");
                        }
                        offset += 8 + size;
                    }
                }
            }
            catch { }
            return null;
        }

        private static long GetDirectorySize(string dir)
        {
            try
            {
                return new DirectoryInfo(dir).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                    .Sum(f => f is FileInfo fi ? fi.Length : 0);
            }
            catch { return 0; }
        }
    }
}
