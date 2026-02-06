using U_Wii_X_Fusion.Settings.Models;

namespace U_Wii_X_Fusion.Settings
{
    public class SettingsManager
    {
        private static SettingsManager _instance;
        private AppSettings _settings;

        private SettingsManager()
        {
            _settings = new AppSettings();
            _settings.Load();
        }

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsManager();
                }
                return _instance;
            }
        }

        public AppSettings GetSettings()
        {
            return _settings;
        }

        public void SaveSettings()
        {
            _settings.Save();
        }

        public void ResetSettings()
        {
            _settings = new AppSettings();
            _settings.Save();
        }
    }
}
