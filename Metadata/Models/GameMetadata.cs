using System;
using System.Collections.Generic;

namespace U_Wii_X_Fusion.Metadata
{
    public class GameMetadata
    {
        public string GameId { get; set; }
        public string Title { get; set; }
        public string Platform { get; set; }
        public string Description { get; set; }
        public string Developer { get; set; }
        public string Publisher { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string Genre { get; set; }
        public string Rating { get; set; }
        public double? Score { get; set; }
        public string CoverUrl { get; set; }
        public string CoverPath { get; set; }
        public List<string> ScreenshotUrls { get; set; }
        public List<string> ScreenshotPaths { get; set; }
        public string TrailerUrl { get; set; }
        public string Region { get; set; }
        public string SerialNumber { get; set; }
    }
}
