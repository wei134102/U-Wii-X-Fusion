using System.Collections.Generic;
using System.Threading.Tasks;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Metadata.Interfaces;
using U_Wii_X_Fusion.Metadata.Providers;

namespace U_Wii_X_Fusion.Metadata
{
    public class MetadataManager
    {
        private readonly List<IMetadataProvider> _providers;

        public MetadataManager()
        {
            _providers = new List<IMetadataProvider>
            {
                new LocalMetadataProvider(),
                new OnlineMetadataProvider()
            };
        }

        public async Task<GameMetadata> GetMetadataAsync(GameInfo game)
        {
            foreach (var provider in _providers)
            {
                if (provider.IsPlatformSupported(game.Platform))
                {
                    var metadata = await provider.GetMetadataAsync(game);
                    if (metadata != null)
                    {
                        return metadata;
                    }
                }
            }
            return null;
        }

        public async Task<GameMetadata> SearchMetadataAsync(string title, string platform)
        {
            foreach (var provider in _providers)
            {
                if (provider.IsPlatformSupported(platform))
                {
                    var metadata = await provider.SearchMetadataAsync(title, platform);
                    if (metadata != null)
                    {
                        return metadata;
                    }
                }
            }
            return null;
        }

        public List<IMetadataProvider> GetProviders()
        {
            return _providers;
        }
    }
}
