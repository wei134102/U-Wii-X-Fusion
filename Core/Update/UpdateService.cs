using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using U_Wii_X_Fusion.Core.Settings;

namespace U_Wii_X_Fusion.Core.Update
{
    /// <summary>GitHub Releases 更新服务</summary>
    public class UpdateService
    {
        private const string GitHubApiBase = "https://api.github.com";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
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
        public async Task<ReleaseInfo> GetLatestReleaseAsync(bool forceRefresh = false)
        {
            try
            {
                var settings = SettingsManager.GetSettings();
                var cached = TryGetCachedRelease(settings);
                if (!forceRefresh && cached != null)
                    return cached;

                // 记录本次检查时间（用于减少启动时重复请求）
                try
                {
                    settings.LastUpdateCheckUtc = DateTime.UtcNow;
                    SettingsManager.UpdateSettings(settings);
                }
                catch { /* 忽略写入失败 */ }

                string url = $"{GitHubApiBase}/repos/{_repoOwner}/{_repoName}/releases/latest";
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "U-Wii-X-Fusion-Updater");
                    client.Headers.Add(HttpRequestHeader.Accept, "application/vnd.github+json");

                    // 如果用户在设置里填写了 ApiKey，这里将其作为 GitHub Token 使用以提高限额（60/h -> 5000/h）
                    if (!string.IsNullOrWhiteSpace(settings?.ApiKey))
                    {
                        var token = settings.ApiKey.Trim();
                        client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
                    }

                    string json = await client.DownloadStringTaskAsync(new Uri(url));
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var release = serializer.Deserialize<GitHubRelease>(json);

                    var info = new ReleaseInfo
                    {
                        Version = release.tag_name?.TrimStart('v', 'V') ?? release.tag_name,
                        TagName = release.tag_name,
                        Name = release.name,
                        Body = release.body,
                        PublishedAt = release.published_at,
                        DownloadUrl = PickBestAssetDownloadUrl(release),
                        DownloadSize = PickBestAssetSize(release)
                    };

                    // 写入缓存（避免频繁触发 GitHub API 限流 403）
                    TryUpdateCachedRelease(settings, info);

                    return info;
                }
            }
            catch (WebException webEx)
            {
                var response = webEx.Response as HttpWebResponse;
                if (response != null)
                {
                    // 403 且 remaining=0 基本就是 rate limit
                    string remaining = response.Headers["X-RateLimit-Remaining"];
                    if (string.Equals(((int)response.StatusCode).ToString(), "403") && remaining == "0")
                    {
                        var resetText = response.Headers["X-RateLimit-Reset"];
                        string resetMsg = string.Empty;
                        if (long.TryParse(resetText, out var resetUnix))
                        {
                            try
                            {
                                var resetAt = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
                                var wait = resetAt - DateTimeOffset.UtcNow;
                                if (wait.TotalSeconds > 0)
                                    resetMsg = $"，预计 {Math.Ceiling(wait.TotalMinutes)} 分钟后恢复";
                            }
                            catch { /* ignore */ }
                        }

                        // 如果有缓存，优先返回缓存（让用户至少能看到上次的结果）
                        var cached = TryGetCachedRelease(SettingsManager.GetSettings(), ignoreTtl: true);
                        if (cached != null)
                        {
                            cached.IsFromCache = true;
                            return cached;
                        }

                        throw new Exception(
                            "GitHub API 限流(403)：未登录情况下每小时最多 60 次请求" + resetMsg +
                            "。你可以在【设置】里填写 GitHub Token（目前使用“ApiKey”输入框）以提升限额到 5000 次/小时。");
                    }
                }
                throw new Exception($"获取更新信息失败: {webEx.Message}", webEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"获取更新信息失败: {ex.Message}", ex);
            }
        }

        private static string PickBestAssetDownloadUrl(GitHubRelease release)
        {
            if (release?.assets == null || release.assets.Length == 0) return null;
            // 优先找 zip 包
            foreach (var a in release.assets)
            {
                if (!string.IsNullOrEmpty(a?.name) && a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return a.browser_download_url;
            }
            return release.assets[0].browser_download_url;
        }

        private static long PickBestAssetSize(GitHubRelease release)
        {
            if (release?.assets == null || release.assets.Length == 0) return 0;
            foreach (var a in release.assets)
            {
                if (!string.IsNullOrEmpty(a?.name) && a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return a.size;
            }
            return release.assets[0].size;
        }

        private static ReleaseInfo TryGetCachedRelease(AppSettings settings, bool ignoreTtl = false)
        {
            if (settings == null) return null;
            if (settings.LatestReleaseCachedAtUtc == null) return null;
            if (!ignoreTtl)
            {
                var age = DateTime.UtcNow - settings.LatestReleaseCachedAtUtc.Value;
                if (age > CacheTtl) return null;
            }

            if (string.IsNullOrWhiteSpace(settings.CachedLatestVersion) &&
                string.IsNullOrWhiteSpace(settings.CachedLatestDownloadUrl))
                return null;

            return new ReleaseInfo
            {
                Version = settings.CachedLatestVersion,
                TagName = settings.CachedLatestTagName,
                Name = settings.CachedLatestName,
                Body = settings.CachedLatestBody,
                PublishedAt = settings.CachedLatestPublishedAt,
                DownloadUrl = settings.CachedLatestDownloadUrl,
                DownloadSize = settings.CachedLatestDownloadSize,
                IsFromCache = true
            };
        }

        private static void TryUpdateCachedRelease(AppSettings settings, ReleaseInfo info)
        {
            if (settings == null || info == null) return;
            try
            {
                settings.LatestReleaseCachedAtUtc = DateTime.UtcNow;
                settings.CachedLatestVersion = info.Version ?? string.Empty;
                settings.CachedLatestTagName = info.TagName ?? string.Empty;
                settings.CachedLatestName = info.Name ?? string.Empty;
                settings.CachedLatestBody = info.Body ?? string.Empty;
                settings.CachedLatestPublishedAt = info.PublishedAt ?? string.Empty;
                settings.CachedLatestDownloadUrl = info.DownloadUrl ?? string.Empty;
                settings.CachedLatestDownloadSize = info.DownloadSize;
                SettingsManager.UpdateSettings(settings);
            }
            catch { /* 忽略缓存写入失败 */ }
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
            // 下载到程序目录下的 CACHE，便于统一管理与清理
            string cacheDir = GetCacheDir();
            string updateDir = Path.Combine(cacheDir, "update");
            if (Directory.Exists(updateDir))
                Directory.Delete(updateDir, true);
            Directory.CreateDirectory(updateDir);

            string zipPath = Path.Combine(updateDir, "update.zip");
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "U-Wii-X-Fusion-Updater");
                client.Headers.Add(HttpRequestHeader.Accept, "application/octet-stream");

                // 私有仓库/提高限流：如果填写了 Token，则带上 Authorization
                var settings = SettingsManager.GetSettings();
                if (!string.IsNullOrWhiteSpace(settings?.ApiKey))
                {
                    var token = settings.ApiKey.Trim();
                    client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
                }

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
            // 解压到 CACHE\update\extracted
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

        /// <summary>创建更新脚本（等待主程序退出后应用更新）</summary>
        public void CreateUpdateScript(string extractedDir, string targetDir, string exePath, int currentPid)
        {
            string cacheDir = GetCacheDir();
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
            string scriptPath = Path.Combine(cacheDir, "apply_update.bat");
            string logPath = Path.Combine(cacheDir, "update.log");

            // 转义路径中的特殊字符，确保批处理脚本正确执行
            string escapedExtract = extractedDir.Replace("\"", "\"\"");
            string escapedTarget = targetDir.Replace("\"", "\"\"");
            string escapedExe = exePath.Replace("\"", "\"\"");
            string escapedCache = cacheDir.Replace("\"", "\"\"");
            string escapedLog = logPath.Replace("\"", "\"\"");
            
            string script = $@"@echo off
chcp 65001 >nul
setlocal enableextensions
set ""EXTRACT_DIR={escapedExtract}""
set ""TARGET_DIR={escapedTarget}""
set ""EXE_PATH={escapedExe}""
set ""CACHE_DIR={escapedCache}""
set ""LOG_PATH={escapedLog}""
set PID={currentPid}

echo [%%date%% %%time%%] start update script > ""%LOG_PATH%""
echo EXTRACT_DIR=%EXTRACT_DIR%>> ""%LOG_PATH%""
echo TARGET_DIR=%TARGET_DIR%>> ""%LOG_PATH%""
echo PID=%PID%>> ""%LOG_PATH%""

REM 等待主程序退出，避免 exe/dll 被占用导致拷贝失败
:wait_exit
tasklist /FI ""PID eq %PID%"" | findstr /R /C:""^ *%PID% "" >nul
if not errorlevel 1 (
  timeout /t 1 /nobreak >nul
  goto wait_exit
)

echo [%%date%% %%time%%] process exited, copying...>> ""%LOG_PATH%""

REM 用 robocopy 更稳定，并排除用户数据目录
robocopy ""%EXTRACT_DIR%"" ""%TARGET_DIR%"" /E /R:3 /W:1 /XD ""Data"" ""CONFIG"" /XF ""settings.json"" ""*.log"" >> ""%LOG_PATH%""
set RC=%%errorlevel%%
REM robocopy 返回码 0-7 都视为成功；>=8 失败
if %RC% GEQ 8 (
  echo [%%date%% %%time%%] copy failed, robocopy errorlevel=%RC%>> ""%LOG_PATH%""
  exit /b 1
)

echo [%%date%% %%time%%] copy ok, restarting...>> ""%LOG_PATH%""
start """" ""%EXE_PATH%""

REM 清理 CACHE（按你的要求：更新完毕后删除 CACHE）
REM 注意：当前脚本位于 CACHE 中，不能直接 rmdir 自己；改为启动一个临时清理脚本
set ""CLEANUP=%TEMP%\\U-Wii-X-Fusion-Cleanup.bat""
echo @echo off> ""%CLEANUP%""
echo chcp 65001 ^>nul>> ""%CLEANUP%""
echo timeout /t 2 /nobreak ^>nul>> ""%CLEANUP%""
echo if exist """"%CACHE_DIR%"""" rmdir /s /q """"%CACHE_DIR%"""" ^>nul 2^>nul>> ""%CLEANUP%""
echo del """"%%~f0"""" ^>nul 2^>nul>> ""%CLEANUP%""
start """" /min cmd.exe /c """"%CLEANUP%""""

del ""%~f0""
";
            File.WriteAllText(scriptPath, script, Encoding.UTF8);
            // 用 cmd.exe 执行 bat（避免直接启动 bat 时窗口/执行策略异常）
            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = cacheDir
            };
            Process.Start(psi);
        }

        private static string GetCacheDir()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CACHE");
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

        /// <summary>是否来自本地缓存（用于 GitHub API 403 限流时兜底）。</summary>
        public bool IsFromCache { get; set; }
    }
}
