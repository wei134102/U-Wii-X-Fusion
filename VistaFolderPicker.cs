using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace U_Wii_X_Fusion
{
    /// <summary>
    /// 使用与“打开文件”同款的 Vista 风格对话框，直接选择文件夹（非“选文件取目录”）。
    /// 在旧系统上回退到 FolderBrowserDialog。
    /// </summary>
    public static class VistaFolderPicker
    {
        public static string PickFolder(string title, string initialPath, Window owner = null)
        {
            IntPtr hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                try
                {
                    string path = ShowVistaDialog(hwnd, title, initialPath);
                    if (!string.IsNullOrEmpty(path)) return path;
                }
                catch { /* 回退到旧对话框 */ }
            }
            return ShowLegacyDialog(title, initialPath, hwnd);
        }

        private static string ShowLegacyDialog(string title, string initialPath, IntPtr ownerHandle)
        {
            var dlg = new FolderBrowserDialog { Description = title };
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                dlg.SelectedPath = initialPath;
            if (ownerHandle != IntPtr.Zero)
            {
                var wrapper = new Win32Window(ownerHandle);
                return dlg.ShowDialog(wrapper) == DialogResult.OK ? dlg.SelectedPath : null;
            }
            return dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedPath : null;
        }

        private static string ShowVistaDialog(IntPtr ownerHandle, string title, string initialPath)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assembly winForms = typeof(FileDialog).Assembly;
            Type iFileDialogType = winForms.GetType("System.Windows.Forms.FileDialogNative+IFileDialog");
            if (iFileDialogType == null) return null;

            MethodInfo createVista = typeof(OpenFileDialog).GetMethod("CreateVistaDialog", flags);
            MethodInfo onBeforeVista = typeof(OpenFileDialog).GetMethod("OnBeforeVistaDialog", flags);
            MethodInfo getOptions = typeof(FileDialog).GetMethod("GetOptions", flags);
            MethodInfo setOptions = iFileDialogType.GetMethod("SetOptions");
            Type fosType = winForms.GetType("System.Windows.Forms.FileDialogNative+FOS");
            if (fosType == null) return null;
            FieldInfo fosPickFolders = fosType.GetField("FOS_PICKFOLDERS");
            if (fosPickFolders == null) return null;
            uint fosPickFoldersValue = (uint)fosPickFolders.GetValue(null);

            Type vistaEventsType = winForms.GetType("System.Windows.Forms.FileDialog+VistaDialogEvents");
            ConstructorInfo vistaEventsCtor = vistaEventsType?.GetConstructor(flags, null, new[] { typeof(FileDialog) }, null);
            MethodInfo advise = iFileDialogType.GetMethod("Advise");
            MethodInfo unadvise = iFileDialogType.GetMethod("Unadvise");
            MethodInfo show = iFileDialogType.GetMethod("Show");

            if (createVista == null || onBeforeVista == null || getOptions == null || setOptions == null ||
                vistaEventsCtor == null || advise == null || unadvise == null || show == null)
                return null;

            var ofd = new OpenFileDialog
            {
                AddExtension = false,
                CheckFileExists = false,
                DereferenceLinks = true,
                Filter = "Folders|\n",
                InitialDirectory = string.IsNullOrEmpty(initialPath) || !Directory.Exists(initialPath) ? null : initialPath,
                Multiselect = false,
                Title = title
            };

            object iFileDialog = createVista.Invoke(ofd, null);
            if (iFileDialog == null) return null;
            onBeforeVista.Invoke(ofd, new[] { iFileDialog });
            uint opts = (uint)getOptions.Invoke(ofd, null);
            setOptions.Invoke(iFileDialog, new object[] { opts | fosPickFoldersValue });

            object events = vistaEventsCtor.Invoke(new object[] { ofd });
            object[] adviseParams = new[] { events, 0U };
            advise.Invoke(iFileDialog, adviseParams);
            try
            {
                int ret = (int)show.Invoke(iFileDialog, new object[] { ownerHandle });
                if (ret == 0 && !string.IsNullOrEmpty(ofd.FileName))
                    return ofd.FileName;
                return null;
            }
            finally
            {
                unadvise.Invoke(iFileDialog, new[] { adviseParams[1] });
            }
        }

        private class Win32Window : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _handle;
            public Win32Window(IntPtr handle) { _handle = handle; }
            public IntPtr Handle => _handle;
        }
    }
}
