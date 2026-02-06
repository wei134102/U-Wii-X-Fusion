using System.Threading.Tasks;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Metadata;

namespace U_Wii_X_Fusion.Metadata.Interfaces
{
    public interface IMetadataProvider
    {
        Task<GameMetadata> GetMetadataAsync(GameInfo game);
        Task<GameMetadata> SearchMetadataAsync(string title, string platform);
        bool IsPlatformSupported(string platform);
    }
}
