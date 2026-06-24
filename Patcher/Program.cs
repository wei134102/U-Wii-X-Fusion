using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Patcher
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Log file in main program directory, not CACHE
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patcher.log");
                
                Log(logPath, $"=== Patcher started at {DateTime.Now} ===");
                
                // Read arguments from response file to avoid command line escaping issues
                string responseFile;
                if (args.Length > 0)
                {
                    responseFile = args[0];
                    Log(logPath, $"Reading arguments from response file: {responseFile}");
                }
                else
                {
                    // Fallback: try to find response file in CACHE
                    string fallbackCacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CACHE");
                    responseFile = Path.Combine(fallbackCacheDir, "patcher_args.txt");
                    Log(logPath, $"No command line argument, using default response file: {responseFile}");
                }
                
                if (!File.Exists(responseFile))
                {
                    Log(logPath, $"ERROR: Response file not found: {responseFile}");
                    return 1;
                }
                
                string[] fileArgs = File.ReadAllLines(responseFile);
                Log(logPath, $"Arguments from file count: {fileArgs.Length}");
                for (int i = 0; i < fileArgs.Length; i++)
                {
                    Log(logPath, $"  args[{i}]: {fileArgs[i]}");
                }
                
                if (fileArgs.Length < 4)
                {
                    Log(logPath, "ERROR: Insufficient arguments in response file. Expected 4 lines");
                    return 1;
                }
                
                string extractedDir = fileArgs[0];
                string targetDir = fileArgs[1];
                string exePath = fileArgs[2];
                int pid;
                
                if (!int.TryParse(fileArgs[3], out pid))
                {
                    Log(logPath, $"ERROR: Invalid PID: {fileArgs[3]}");
                    return 1;
                }
                
                Log(logPath, $"ExtractedDir: {extractedDir}");
                Log(logPath, $"TargetDir: {targetDir}");
                Log(logPath, $"ExePath: {exePath}");
                Log(logPath, $"PID: {pid}");
                
                // Wait for main process to exit
                Log(logPath, "Waiting for main process to exit...");
                WaitForProcessExit(pid, logPath);
                Log(logPath, "Main process has exited");
                
                // Copy update files
                Log(logPath, "Starting file copy...");
                CopyUpdateFiles(extractedDir, targetDir, logPath);
                Log(logPath, "File copy completed");
                
                // Restart main program
                Log(logPath, $"Restarting main program: {exePath}");
                Process.Start(exePath);
                Log(logPath, "Main program restarted");
                
                // Clean up cache
                Log(logPath, "Cleaning up cache directory...");
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CACHE");
                try
                {
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        Log(logPath, "Cache directory deleted");
                    }
                }
                catch (Exception ex)
                {
                    Log(logPath, $"WARNING: Failed to delete cache directory: {ex.Message}");
                }
                
                Log(logPath, $"=== Patcher completed successfully at {DateTime.Now} ===");
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CACHE", "patcher.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    Log(logPath, $"FATAL ERROR: {ex.Message}");
                    Log(logPath, $"Stack trace: {ex.StackTrace}");
                }
                catch { }
                return 1;
            }
        }
        
        static void WaitForProcessExit(int pid, string logPath)
        {
            int maxWaitSeconds = 60;
            int waitedSeconds = 0;
            
            while (waitedSeconds < maxWaitSeconds)
            {
                try
                {
                    Process process = Process.GetProcessById(pid);
                    if (process.HasExited)
                    {
                        Log(logPath, $"Process {pid} has exited");
                        return;
                    }
                }
                catch (ArgumentException)
                {
                    Log(logPath, $"Process {pid} not found (already exited)");
                    return;
                }
                
                System.Threading.Thread.Sleep(1000);
                waitedSeconds++;
            }
            
            Log(logPath, $"WARNING: Process {pid} did not exit within {maxWaitSeconds} seconds, proceeding anyway");
        }
        
        static void CopyUpdateFiles(string extractedDir, string targetDir, string logPath)
        {
            string[] excludeDirs = { "Data", "CONFIG", "CACHE" };
            string[] excludeFiles = { "settings.json", "Patcher.exe", "Patcher.pdb" };
            
            if (!Directory.Exists(extractedDir))
            {
                throw new DirectoryNotFoundException($"Extracted directory not found: {extractedDir}");
            }
            
            if (!Directory.Exists(targetDir))
            {
                throw new DirectoryNotFoundException($"Target directory not found: {targetDir}");
            }
            
            foreach (string file in Directory.GetFiles(extractedDir, "*.*", SearchOption.AllDirectories))
            {
                string relPath = file.Substring(extractedDir.Length).TrimStart(Path.DirectorySeparatorChar);
                string targetPath = Path.Combine(targetDir, relPath);
                string targetDirPath = Path.GetDirectoryName(targetPath);
                
                // Skip excluded directories
                bool skip = false;
                foreach (var exclude in excludeDirs)
                {
                    if (relPath.StartsWith(exclude + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        relPath.Equals(exclude, StringComparison.OrdinalIgnoreCase))
                    {
                        skip = true;
                        break;
                    }
                }
                
                if (skip)
                {
                    Log(logPath, $"Skipping directory: {relPath}");
                    continue;
                }
                
                // Skip excluded files
                foreach (var exclude in excludeFiles)
                {
                    if (Path.GetFileName(relPath).Equals(exclude, StringComparison.OrdinalIgnoreCase))
                    {
                        skip = true;
                        break;
                    }
                }
                
                if (skip)
                {
                    Log(logPath, $"Skipping file: {relPath}");
                    continue;
                }
                
                // Skip log files
                if (relPath.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                {
                    Log(logPath, $"Skipping log file: {relPath}");
                    continue;
                }
                
                // Create target directory if needed
                if (!Directory.Exists(targetDirPath))
                {
                    Directory.CreateDirectory(targetDirPath);
                    Log(logPath, $"Created directory: {relPath}");
                }
                
                // Copy file
                try
                {
                    File.Copy(file, targetPath, overwrite: true);
                    Log(logPath, $"Copied: {relPath}");
                }
                catch (Exception ex)
                {
                    Log(logPath, $"ERROR copying {relPath}: {ex.Message}");
                    throw;
                }
            }
        }
        
        static void Log(string logPath, string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                Console.WriteLine(logMessage);
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
            catch { }
        }
    }
}
