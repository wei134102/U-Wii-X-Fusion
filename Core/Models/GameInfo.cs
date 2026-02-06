using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace U_Wii_X_Fusion.Core.Models
{
    public class GameInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string GameId { get; set; }
        public string Title { get; set; }
        public string ChineseTitle { get; set; }
        public string Platform { get; set; }
        public string PlatformType { get; set; }
        public string Format { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }

        /// <summary>用于界面显示的文件大小（如 "1.35 GB"）</summary>
        public string FormattedSize
        {
            get
            {
                if (Size <= 0) return "0 B";
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                int unitIndex = 0;
                double size = Size;
                while (size >= 1024 && unitIndex < units.Length - 1)
                {
                    size /= 1024;
                    unitIndex++;
                }
                return $"{size:F2} {units[unitIndex]}";
            }
        }

        public string Region { get; set; }
        public string Publisher { get; set; }
        public string Developer { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string Status { get; set; }
        public string CoverPath { get; set; }
        /// <summary>来自 Wii 游戏数据库（wiitdb.xml）的简介 / 剧情说明（synopsis）</summary>
        public string Synopsis { get; set; }

        private bool _isSelected;
        /// <summary>列表中是否勾选（用于多选、保存列表、拷贝等）</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

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
