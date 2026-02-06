using System;
using System.IO;
using System.Text;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    /// <summary>
    /// 读取 Wii / NGC 光盘镜像的头部信息，用于从文件内容中解析 GameID 和标题。
    /// 参考了社区对 GameCube/Wii 光盘头结构的约定：
    /// - 偏移 0x000: 6 字节 GameID（ID4/ID6）
    /// - 偏移 0x020: 标题字符串（ASCII，最多 64 字节，0 结尾）
    /// - 对非标准 WBFS 文件：GameID 存放在偏移 0x200 处的 6 字节（由用户提供经验）
    /// </summary>
    internal static class DiscHeaderReader
    {
        /// <summary>
        /// 尝试从给定的光盘镜像文件中读取 GameID 和标题。
        /// 仅对原盘格式（ISO/GCM 等）有效，WBFS 当前仍主要依赖文件名/目录名。
        /// </summary>
        /// <param name="filePath">镜像文件路径（.iso / .gcm 等）</param>
        /// <param name="gameId">读取到的 GameID（ID4/ID6）</param>
        /// <param name="title">读取到的标题（如无有效标题则为空）</param>
        /// <param name="isWii">是否识别为 Wii 光盘</param>
        /// <param name="isGameCube">是否识别为 GameCube 光盘</param>
        /// <returns>成功解析则为 true，否则 false</returns>
        public static bool TryReadDiscHeader(string filePath, out string gameId, out string title, out bool isWii, out bool isGameCube)
        {
            gameId = null;
            title = null;
            isWii = false;
            isGameCube = false;

            try
            {
                var fileInfo = new FileInfo(filePath);
                // 头部很小，但至少要有 0x60 字节比较安全
                if (!fileInfo.Exists || fileInfo.Length < 0x40)
                    return false;

                byte[] header = new byte[0x60];
                int read;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    read = fs.Read(header, 0, header.Length);
                }

                if (read < 8)
                    return false;

                // 前 6 字节为 ID6（对于部分标题可能只用前 4 字节）
                string idCandidate = Encoding.ASCII.GetString(header, 0, 6)
                    .TrimEnd('\0', ' ', '\r', '\n', '\t');

                if (idCandidate.Length < 4)
                    return false;

                // 简单校验：前 4 字符必须是可见的大写字母/数字
                bool idValid = true;
                int checkLen = Math.Min(4, idCandidate.Length);
                for (int i = 0; i < checkLen; i++)
                {
                    char c = idCandidate[i];
                    if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'Z'))
                    {
                        idValid = false;
                        break;
                    }
                }
                if (!idValid)
                    return false;

                gameId = idCandidate;

                // 标题通常在偏移 0x20，ASCII 字符串，末尾以 0 填充
                if (read >= 0x40)
                {
                    const int titleOffset = 0x20;
                    const int titleLength = 0x40; // 64 字节足够
                    int available = Math.Min(titleLength, read - titleOffset);
                    if (available > 0)
                    {
                        string rawTitle = Encoding.ASCII.GetString(header, titleOffset, available)
                            .TrimEnd('\0', ' ', '\r', '\n', '\t');
                        if (!string.IsNullOrWhiteSpace(rawTitle))
                            title = rawTitle;
                    }
                }

                // 通过 ID 首字符粗略判断平台
                // 参考 TinyWiiBackupManager 中 GameID 的 is_wii/is_gc 判断
                char systemChar = char.ToUpperInvariant((char)header[0]);
                isGameCube = systemChar == 'D' || systemChar == 'G';
                isWii = systemChar == 'H' || systemChar == 'R' || systemChar == 'S' || systemChar == 'W' || systemChar == 'X';

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试从 WBFS 文件中读取 GameID。
        /// 根据你的说明：非标准 WBFS 文件的 ID 存放在偏移 0x200（512）开始的 6 字节。
        /// 这里只解析 ID，不解析标题。
        /// </summary>
        public static bool TryReadWbfsGameId(string filePath, out string gameId)
        {
            gameId = null;
            try
            {
                var fileInfo = new FileInfo(filePath);
                const long offset = 0x200;
                const int length = 6;

                if (!fileInfo.Exists || fileInfo.Length < offset + length)
                    return false;

                byte[] idBytes = new byte[length];
                int read;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    read = fs.Read(idBytes, 0, idBytes.Length);
                }

                if (read < 4)
                    return false;

                string idCandidate = Encoding.ASCII.GetString(idBytes, 0, read)
                    .TrimEnd('\0', ' ', '\r', '\n', '\t');

                if (idCandidate.Length < 4)
                    return false;

                bool idValid = true;
                int checkLen = Math.Min(4, idCandidate.Length);
                for (int i = 0; i < checkLen; i++)
                {
                    char c = idCandidate[i];
                    if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'Z'))
                    {
                        idValid = false;
                        break;
                    }
                }
                if (!idValid)
                    return false;

                gameId = idCandidate;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

