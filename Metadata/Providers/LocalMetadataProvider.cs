using System.Collections.Generic;
using System.Threading.Tasks;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Metadata;
using U_Wii_X_Fusion.Metadata.Interfaces;

namespace U_Wii_X_Fusion.Metadata.Providers
{
    public class LocalMetadataProvider : IMetadataProvider
    {
        private readonly Dictionary<string, GameMetadata> _localMetadata;

        public LocalMetadataProvider()
        {
            _localMetadata = new Dictionary<string, GameMetadata>();
            // 初始化一些模拟元数据
            InitializeMockMetadata();
        }

        private void InitializeMockMetadata()
        {
            // 添加一些模拟元数据用于测试
            _localMetadata["wii-supermario"] = new GameMetadata
            {
                GameId = "wii-supermario",
                Title = "Super Mario Galaxy",
                Platform = "Wii",
                Description = "Super Mario Galaxy is a 2007 platform action-adventure video game developed and published by Nintendo for the Wii.",
                Developer = "Nintendo EAD",
                Publisher = "Nintendo",
                Genre = "Platformer",
                Rating = "E",
                Score = 97,
                CoverUrl = "https://example.com/supermario.jpg",
                Region = "USA"
            };

            _localMetadata["wiiu-mariokart"] = new GameMetadata
            {
                GameId = "wiiu-mariokart",
                Title = "Mario Kart 8",
                Platform = "Wii U",
                Description = "Mario Kart 8 is a 2014 kart racing game developed and published by Nintendo for the Wii U.",
                Developer = "Nintendo EAD",
                Publisher = "Nintendo",
                Genre = "Racing",
                Rating = "E",
                Score = 88,
                CoverUrl = "https://example.com/mariokart.jpg",
                Region = "USA"
            };

            _localMetadata["xbox360-halo"] = new GameMetadata
            {
                GameId = "xbox360-halo",
                Title = "Halo 3",
                Platform = "Xbox 360",
                Description = "Halo 3 is a 2007 first-person shooter video game developed by Bungie for the Xbox 360 console.",
                Developer = "Bungie",
                Publisher = "Microsoft Game Studios",
                Genre = "First-person shooter",
                Rating = "M",
                Score = 94,
                CoverUrl = "https://example.com/halo.jpg",
                Region = "USA"
            };
        }

        public async Task<GameMetadata> GetMetadataAsync(GameInfo game)
        {
            // 模拟异步操作
            await Task.Delay(100);

            string key = $"{game.Platform.ToLower()}-{game.Title.ToLower().Replace(' ', '-')}";
            if (_localMetadata.TryGetValue(key, out var metadata))
            {
                return metadata;
            }

            return null;
        }

        public async Task<GameMetadata> SearchMetadataAsync(string title, string platform)
        {
            // 模拟异步操作
            await Task.Delay(100);

            string key = $"{platform.ToLower()}-{title.ToLower().Replace(' ', '-')}";
            if (_localMetadata.TryGetValue(key, out var metadata))
            {
                return metadata;
            }

            return null;
        }

        public bool IsPlatformSupported(string platform)
        {
            return true; // 本地提供器支持所有平台
        }
    }
}
