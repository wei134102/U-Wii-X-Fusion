using System.Net.Http;
using System.Threading.Tasks;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Metadata;
using U_Wii_X_Fusion.Metadata.Interfaces;

namespace U_Wii_X_Fusion.Metadata.Providers
{
    public class OnlineMetadataProvider : IMetadataProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public OnlineMetadataProvider(string apiKey = null)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey ?? "demo-api-key";
        }

        public async Task<GameMetadata> GetMetadataAsync(GameInfo game)
        {
            try
            {
                // 模拟API调用
                await Task.Delay(500);

                // 这里应该是实际的API调用，现在返回模拟数据
                return new GameMetadata
                {
                    GameId = $"online-{game.Platform.ToLower()}-{game.Title.ToLower().Replace(' ', '-')}",
                    Title = game.Title,
                    Platform = game.Platform,
                    Description = $"Online metadata for {game.Title} on {game.Platform}",
                    Developer = "Unknown",
                    Publisher = "Unknown",
                    Genre = "Unknown",
                    Rating = "Unknown",
                    Score = 80,
                    CoverUrl = $"https://example.com/covers/{game.Platform.ToLower()}/{game.Title.ToLower().Replace(' ', '-')}.jpg",
                    Region = "Unknown"
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<GameMetadata> SearchMetadataAsync(string title, string platform)
        {
            try
            {
                // 模拟API调用
                await Task.Delay(500);

                // 这里应该是实际的API调用，现在返回模拟数据
                return new GameMetadata
                {
                    GameId = $"online-{platform.ToLower()}-{title.ToLower().Replace(' ', '-')}",
                    Title = title,
                    Platform = platform,
                    Description = $"Online metadata for {title} on {platform}",
                    Developer = "Unknown",
                    Publisher = "Unknown",
                    Genre = "Unknown",
                    Rating = "Unknown",
                    Score = 80,
                    CoverUrl = $"https://example.com/covers/{platform.ToLower()}/{title.ToLower().Replace(' ', '-')}.jpg",
                    Region = "Unknown"
                };
            }
            catch
            {
                return null;
            }
        }

        public bool IsPlatformSupported(string platform)
        {
            // 模拟支持的平台
            return platform == "Wii" || platform == "Wii U" || platform == "Xbox 360";
        }
    }
}
