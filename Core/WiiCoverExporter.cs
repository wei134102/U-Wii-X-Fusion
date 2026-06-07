using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using U_Wii_X_Fusion.Core.Settings;

namespace U_Wii_X_Fusion.Core
{
    /// <summary>导出 Wii 封面（2D / 3D / disc / full）到指定目录。</summary>
    public static class WiiCoverExporter
    {
        private static readonly string[] CoverTypeFolders = { "2d", "3d", "disc", "full" };
        private static readonly string[] CoverExtensions = { ".png", ".jpg" };

        public sealed class ExportResult
        {
            public int GameCount { get; set; }
            public int CopiedCount { get; set; }
            public int MissingGameCount { get; set; }
            public int FailedCount { get; set; }
            public string ExportDirectory { get; set; }
        }

        /// <summary>
        /// 选择导出目录。若上次目录仍有效，询问使用上次目录或选择新目录；否则直接弹出目录选择。
        /// </summary>
        public static string PickExportDirectory(Window owner)
        {
            var settings = SettingsManager.GetSettings();
            string lastPath = settings.LastCoverExportPath;

            if (!string.IsNullOrEmpty(lastPath) && Directory.Exists(lastPath))
            {
                var r = MessageBox.Show(
                    AppLanguage.L(
                        $"上次导出目录：\n{lastPath}\n\n是否使用此目录？\n\n是 — 使用上次目录\n否 — 选择新目录\n取消 — 放弃导出",
                        $"Last export directory:\n{lastPath}\n\nUse this directory?\n\nYes — use last directory\nNo — choose a new directory\nCancel — abort export"),
                    AppLanguage.L("选择导出目录", "Select export directory"),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return null;
                if (r == MessageBoxResult.Yes) return lastPath;
            }

            string picked = VistaFolderPicker.PickFolder(
                AppLanguage.L("选择封面导出目录", "Select cover export directory"),
                lastPath,
                owner);
            if (string.IsNullOrEmpty(picked)) return null;

            settings.LastCoverExportPath = picked;
            SettingsManager.UpdateSettings(settings);
            return picked;
        }

        public static string ResolveCoverPath(string coverBasePath, string gameId, string typeFolder)
        {
            if (string.IsNullOrEmpty(coverBasePath) || string.IsNullOrEmpty(gameId))
                return null;

            foreach (string ext in CoverExtensions)
            {
                string fileName = gameId + ext;
                string path = Path.Combine(coverBasePath, typeFolder.ToLowerInvariant(), fileName);
                if (File.Exists(path)) return path;
                string cap = char.ToUpperInvariant(typeFolder[0]) + typeFolder.Substring(1).ToLowerInvariant();
                path = Path.Combine(coverBasePath, cap, fileName);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static ExportResult ExportCovers(IEnumerable<string> gameIds, string coverBasePath, string exportDirectory)
        {
            var result = new ExportResult { ExportDirectory = exportDirectory };
            if (string.IsNullOrEmpty(coverBasePath) || string.IsNullOrEmpty(exportDirectory))
                return result;

            var ids = (gameIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.GameCount = ids.Count;

            foreach (string gameId in ids)
            {
                bool foundAny = false;
                foreach (string typeFolder in CoverTypeFolders)
                {
                    string src = ResolveCoverPath(coverBasePath, gameId, typeFolder);
                    if (src == null) continue;

                    foundAny = true;
                    string srcTypeDir = Path.GetFileName(Path.GetDirectoryName(src));
                    string destDir = Path.Combine(exportDirectory, srcTypeDir);
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    string destPath = Path.Combine(destDir, Path.GetFileName(src));
                    try
                    {
                        File.Copy(src, destPath, overwrite: true);
                        result.CopiedCount++;
                    }
                    catch
                    {
                        result.FailedCount++;
                    }
                }
                if (!foundAny)
                    result.MissingGameCount++;
            }
            return result;
        }

        public static void ShowResultMessage(ExportResult result)
        {
            if (result == null) return;

            string msg = AppLanguage.L(
                $"已为 {result.GameCount} 个游戏导出 {result.CopiedCount} 张封面到：\n{result.ExportDirectory}",
                $"Exported {result.CopiedCount} cover(s) for {result.GameCount} game(s) to:\n{result.ExportDirectory}");

            if (result.MissingGameCount > 0)
            {
                msg += AppLanguage.L(
                    $"\n\n{result.MissingGameCount} 个游戏未找到任何封面。",
                    $"\n\nNo covers found for {result.MissingGameCount} game(s).");
            }
            if (result.FailedCount > 0)
            {
                msg += AppLanguage.L(
                    $"\n{result.FailedCount} 个文件复制失败。",
                    $"\n{result.FailedCount} file(s) failed to copy.");
            }

            MessageBox.Show(
                msg,
                AppLanguage.L("导出封面", "Export covers"),
                MessageBoxButton.OK,
                result.CopiedCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }
}
