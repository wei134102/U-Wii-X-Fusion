using U_Wii_X_Fusion.Core.Settings;

namespace U_Wii_X_Fusion
{
    /// <summary>全局界面语言：根据设置返回中文或英文</summary>
    public static class AppLanguage
    {
        public static bool IsEnglish => SettingsManager.GetSettings().UseEnglish;

        public static string L(string zh, string en) => IsEnglish ? en : zh;
    }
}
