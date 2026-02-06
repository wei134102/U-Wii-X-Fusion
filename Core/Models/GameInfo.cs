using System;
using System.Collections.Generic;

namespace U_Wii_X_Fusion.Core.Models
{
    public class GameInfo
    {
        public string GameId { get; set; }
        public string Title { get; set; }
        public string ChineseTitle { get; set; }
        public string Platform { get; set; }
        public string PlatformType { get; set; }
        public string Format { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string Region { get; set; }
        public string Publisher { get; set; }
        public string Developer { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string Status { get; set; }
        public string CoverPath { get; set; }
        public int Players { get; set; }
        public List<string> Controllers { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Languages { get; set; }

        public GameInfo()
        {
            Controllers = new List<string>();
            Genres = new List<string>();
            Languages = new List<string>();
        }
    }
}
