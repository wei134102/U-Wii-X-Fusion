using System.IO;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.Interfaces
{
    public interface IGameIdentifier
    {
        GameInfo IdentifyGame(string filePath);
        bool IsSupportedFormat(string filePath);
        string GetPlatform();
    }
}
