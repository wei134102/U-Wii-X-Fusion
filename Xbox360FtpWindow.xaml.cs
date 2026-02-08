using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using U_Wii_X_Fusion.Core;

namespace U_Wii_X_Fusion
{
    public partial class Xbox360FtpWindow : Window
    {
        private readonly Xbox360FtpOperations _ftp;
        private BackgroundWorker _worker;

        public Xbox360FtpWindow()
        {
            InitializeComponent();
            var icon = App.GetWindowIcon();
            if (icon != null) Icon = icon;
            _ftp = new Xbox360FtpOperations();
            _ftp.StatusChanged += (s, msg) =>
            {
                string m = msg;
                if (!Dispatcher.CheckAccess())
                    Dispatcher.BeginInvoke(new Action(() => { txtStatus.Text = m; }));
                else
                    txtStatus.Text = m;
            };
            if (_ftp.HaveSettings)
            {
                txtIp.Text = _ftp.IpAddress;
                txtPort.Text = _ftp.Port;
                txtUser.Text = _ftp.Username;
                txtPass.Text = _ftp.Password;
            }
            txtStatus.Text = _ftp.HaveSettings ? "已加载保存的 FTP 设置。" : "请填写 IP、用户名、密码（默认 xboxftp）并测试连接。";
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            string ip = txtIp.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("请输入 IP 地址。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // 在 UI 线程先取好值，避免在 DoWork 里访问控件
            string user = txtUser.Text ?? "";
            string pass = txtPass.Text ?? "";
            string port = txtPort.Text?.Trim() ?? "21";
            txtStatus.Text = "正在测试连接...";
            _worker = new BackgroundWorker();
            _worker.DoWork += (o, args) =>
            {
                args.Result = _ftp.TestConnection(ip, user, pass, port);
            };
            _worker.RunWorkerCompleted += (o, args) =>
            {
                bool ok = args.Error == null && (args.Result as bool? == true);
                if (ok)
                    MessageBox.Show("连接成功。", "FTP", MessageBoxButton.OK, MessageBoxImage.Information);
                else if (args.Error != null)
                    MessageBox.Show("连接失败: " + args.Error.Message, "FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
            };
            _worker.RunWorkerAsync();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _ftp.SaveSettings(txtIp.Text?.Trim(), txtUser.Text, txtPass.Text, txtPort.Text?.Trim() ?? "21");
            txtStatus.Text = "FTP 设置已保存。";
            MessageBox.Show("设置已保存。", "FTP", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDownloadDb_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择保存数据库的文件夹（将下载 settings.db 和 Content.db 到该文件夹）";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                string folder = dlg.SelectedPath;
                txtStatus.Text = "正在下载数据库...";
                _worker = new BackgroundWorker();
                _worker.DoWork += (o, args) =>
                {
                    try
                    {
                        _ftp.DownloadDatabases(folder, out bool contentOk, out bool settingsOk);
                        args.Result = new Tuple<bool, bool>(contentOk, settingsOk);
                    }
                    catch (Exception ex)
                    {
                        args.Result = ex;
                    }
                };
                _worker.RunWorkerCompleted += (o, args) =>
                {
                    if (args.Result is Exception ex)
                    {
                        MessageBox.Show("下载失败: " + ex.Message, "FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
                        txtStatus.Text = "下载失败: " + ex.Message;
                        return;
                    }
                    var t = args.Result as Tuple<bool, bool>;
                    if (t == null) return;
                    bool contentOk = t.Item1;
                    bool settingsOk = t.Item2;
                    var parts = new System.Collections.Generic.List<string>();
                    if (contentOk) parts.Add("Content.db");
                    if (settingsOk) parts.Add("settings.db");
                    if (parts.Count == 0)
                    {
                        txtStatus.Text = "未下载到任何文件。";
                        MessageBox.Show("未能下载 settings.db 或 Content.db，请确认主机已开启 FTP 且 /Game/Data/DataBases/ 下存在相应文件。", "FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        txtStatus.Text = "已保存: " + string.Join("、", parts);
                        bool openViewer = MessageBox.Show("已保存到:\n" + folder + "\n\n" + string.Join("、", parts) + "\n\n是否打开数据库查看？", "下载数据库", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;
                        if (openViewer)
                        {
                            var viewer = new AuroraDatabaseWindow { Owner = Owner };
                            viewer.OpenFolder(folder);
                            viewer.Show();
                        }
                    }
                };
                _worker.RunWorkerAsync();
            }
        }

        private void BtnOpenDbViewer_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择包含 Content.db、settings.db 的文件夹";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var win = new AuroraDatabaseWindow { Owner = this };
                win.OpenFolder(dlg.SelectedPath);
                win.Show();
            }
        }

        private void BtnListDirs_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "正在获取 GameData 目录列表...";
            lstDirs.Items.Clear();
            _worker = new BackgroundWorker();
            _worker.DoWork += (o, args) =>
            {
                try
                {
                    if (!_ftp.NavigateToGameDataDir())
                    {
                        args.Result = new string[0];
                        return;
                    }
                    args.Result = _ftp.GetDirList();
                }
                catch (Exception ex)
                {
                    args.Result = ex;
                }
            };
            _worker.RunWorkerCompleted += (o, args) =>
            {
                if (args.Result is Exception ex)
                {
                    txtStatus.Text = "获取失败: " + ex.Message;
                    MessageBox.Show("获取目录列表失败: " + ex.Message, "FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var dirs = args.Result as string[];
                if (dirs != null)
                {
                    foreach (var d in dirs)
                        lstDirs.Items.Add(d);
                    txtStatus.Text = string.Format("共 {0} 个目录。", dirs.Length);
                }
            };
            _worker.RunWorkerAsync();
        }
    }
}
