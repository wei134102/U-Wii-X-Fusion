using System.ComponentModel;

namespace U_Wii_X_Fusion
{
    /// <summary>插件项：用于左侧列表与解析 INI [PLUGIN]</summary>
    public class PluginItem : INotifyPropertyChanged
    {
        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); }
        }
        public string FilePath { get; set; }
        public string FileTypes { get; set; }
        public string RomDir { get; set; }
        public string CoverFolder { get; set; }
        public string Magic { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>ROM 行：文件名、显示名称、是否缺图（用于标红）</summary>
    public class RomRow : INotifyPropertyChanged
    {
        private string _fileName;
        private string _displayName;
        private bool _hasImage;
        public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(nameof(FileName)); } }
        public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); } }
        public bool HasImage { get => _hasImage; set { _hasImage = value; OnPropertyChanged(nameof(HasImage)); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>图片行：文件名、分辨率</summary>
    public class ImageRow
    {
        public string FileName { get; set; }
        public string Resolution { get; set; }
    }
}
