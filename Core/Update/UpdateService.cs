using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace U_Wii_X_Fusion.Core.Update
{
    /// <summary>GitHub Releases 更新服务</summary>
    public class UpdateService
    {
        private const string GitHubApiBase = "https://api.github.com";
        private readonly string _repoOwner;
        private readonly string _repoName;
        private readonly string _currentVersion;

        public UpdateService(string repoOwner, string repoName)
        {
            _repoOwner = repoOwner;
            _repoName = repoName;
            _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        }

        /// <summary>获取当前程序版本</summary>
        public string CurrentVersion => _currentVersion;

        /// <summary>从 GitHub Releases API 获取最新版本信息</summary>
        public async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            try
            {
                string url = $"{GitHubApiBase}/repos/{_repoOwner}/{_repoName}/releases/latest";
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "U-Wii-X-Fusion-Updater");
                    string json = await client.DownloadStringTaskAsync(new Uri(url));
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var release = serializer.Deserialize<GitHubRelease>(json);
                    return new ReleaseInfo
                    {
                        Version = release.tag_name?.TrimStart('v', 'V') ?? release.tag_name,
                        TagName = release.tag_name,
                        Name = release.name,
                        Body = release.body,
                        PublishedAt = release.published_at,
                        DownloadUrl = release.assets != null && release.assets.Length > 0 ? release.assets[0].browser_download_url : null,
                        DownloadSize = release.assets != null && release.assets.Length > 0 ? release.assets[0].size : 0
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取更新信息失败: {ex.Message}", ex);
            }
        }

        /// <summary>比较版本号（如 "1.0.0" vs "1.0.0.0"）</summary>
        public bool IsNewerVersion(string remoteVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(remoteVersion)) return false;
            try
            {
                var remote = Version.Parse(remoteVersion);
                var current = Version.Parse(currentVersion);
                return remote > current;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>下载更新包到临时目录</summary>
        public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<int> progress = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "U-Wii-X-Fusion-Update");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "U-Wii-X-Fusion-Updater");
                if (progress != null)
                {
                    client.DownloadProgressChanged += (s, e) => progress.Report(e.ProgressPercentage);
                }
                await client.DownloadFileTaskAsync(new Uri(downloadUrl), zipPath);
            }
            return zipPath;
        }

        /// <summary>解压更新包到临时目录</summary>
        public string ExtractUpdate(string zipPath)
        {
            string extractDir = Path.Combine(Path.GetDirectoryName(zipPath), "extracted");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            return extractDir;
        }

        /// <summary>应用更新：将更新文件复制到程序目录（需要管理员权限或程序目录可写）</summary>
        public void ApplyUpdate(string extractedDir, string targetDir)
        {
            string[] excludeDirs = { "CONFIG", "Data" };
            string[] excludeFiles = { "settings.json", "*.log" };

            foreach (string file in Directory.GetFiles(extractedDir, "*.*", SearchOption.AllDirectories))
            {
                string relPath = file.Substring(extractedDir.Length + 1);
                string targetPath = Path.Combine(targetDir, relPath);
                string targetDirPath = Path.GetDirectoryName(targetPath);

                bool skip = false;
                foreach (var exclude in excludeDirs)
                {
                    if (relPath.StartsWith(exclude + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        skip = true;
                        break;
                    }
                }
                if (skip) continue;

                if (!Directory.Exists(targetDirPath))
                    Directory.CreateDirectory(targetDirPath);

                try
                {
                    File.Copy(file, targetPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    throw new Exception($"复制文件失败 {relPath}: {ex.Message}", ex);
                }
            }
        }

        /// <summary>创建更新脚本（用于重启后应用更新）</summary>
        public void CreateUpdateScript(string extractedDir, string targetDir, string exePath)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "U-Wii-X-Fusion-Update.bat");
            // 转义路径中的特殊字符，确保批处理脚本正确执行
            string escapedExtract = extractedDir.Replace("\"", "\"\"");
            string escapedTarget = targetDir.Replace("\"", "\"\"");
            string escapedExe = exePath.Replace("\"", "\"\"");
            
            string script = $@"@echo off
chcp 65001 >nul
timeout /t 2 /nobreak >nul
xcopy /E /Y /I ""{escapedExtract}\*"" ""{escapedTarget}\""
if errorlevel 1 (
    echo 更新失败，请手动复制文件
    pause
    exit /b 1
)
start """" ""{escapedExe}""
timeout /t 1 /nobreak >nul
del ""%~f0""
";
            File.WriteAllText(scriptPath, script, Encoding.UTF8);
            Process.Start(new ProcessStartInfo(scriptPath) { WindowStyle = ProcessWindowStyle.Hidden });
        }
    }

    /// <summary>GitHub Release API 响应模型</summary>
    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public string published_at { get; set; }
        public GitHubAsset[] assets { get; set; }
    }

    public class GitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }

    /// <summary>发布信息</summary>
    public class ReleaseInfo
    {
        public string Version { get; set; }
        public string TagName { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        public string PublishedAt { get; set; }
        public string DownloadUrl { get; set; }
        public long DownloadSize { get; set; }
    }
}
