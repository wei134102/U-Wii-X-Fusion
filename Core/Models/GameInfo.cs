using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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

        /// <summary>游戏所在文件夹的父文件夹名称（用于 Wii U 列表“文件夹”列）</summary>
        public string ParentFolderName
        {
            get
            {
                if (string.IsNullOrEmpty(Path)) return string.Empty;
                string gameDir = Directory.Exists(Path) ? Path : System.IO.Path.GetDirectoryName(Path);
                if (string.IsNullOrEmpty(gameDir)) return string.Empty;
                string parent = System.IO.Path.GetDirectoryName(gameDir);
                return string.IsNullOrEmpty(parent) ? string.Empty : System.IO.Path.GetFileName(parent);
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
        /// <summary>分类/类型（如 Shooter、Action），用于 Xbox 360 等，取自 Genres 或 Category 字段</summary>
        public string Category => Genres != null && Genres.Count > 0 ? string.Join(", ", Genres) : "";
        public List<string> Languages { get; set; }

        public GameInfo()
        {
            Controllers = new List<string>();
            Genres = new List<string>();
            Languages = new List<string>();
        }
    }
}
