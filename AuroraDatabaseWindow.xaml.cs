using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace U_Wii_X_Fusion
{
    public partial class AuroraDatabaseWindow : Window
    {
        private string _currentDbPath;
        private string _dbFolder;
        private DataTable _currentTable;
        private string _currentTableName;
        private readonly List<TableItem> _tableItems = new List<TableItem>();
        private string _luaFolder;

        public AuroraDatabaseWindow()
        {
            InitializeComponent();
            var icon = App.GetWindowIcon();
            if (icon != null) Icon = icon;
            _luaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "lua");
        }

        /// <summary>使用指定文件夹打开（Content.db、settings.db 所在目录）</summary>
        public void OpenFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;
            _dbFolder = Path.GetFullPath(folderPath);
            cmbDbFile.Items.Clear();
            string contentDb = Path.Combine(_dbFolder, "Content.db");
            string settingsDb = Path.Combine(_dbFolder, "settings.db");
            if (File.Exists(contentDb))
                cmbDbFile.Items.Add(new DbFileItem { Path = contentDb, Display = "Content.db" });
            if (File.Exists(settingsDb))
                cmbDbFile.Items.Add(new DbFileItem { Path = settingsDb, Display = "settings.db" });
            if (cmbDbFile.Items.Count > 0)
                cmbDbFile.SelectedIndex = 0;
            else
                txtStatus.Text = "该文件夹下未找到 Content.db 或 settings.db。";
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "数据库|*.db|所有文件|*.*",
                Title = "选择 Content.db 或 settings.db"
            };
            if (dlg.ShowDialog() == true)
            {
                _dbFolder = Path.GetDirectoryName(dlg.FileName);
                cmbDbFile.Items.Clear();
                cmbDbFile.Items.Add(new DbFileItem { Path = dlg.FileName, Display = Path.GetFileName(dlg.FileName) });
                string other = Path.GetFileName(dlg.FileName).Equals("Content.db", StringComparison.OrdinalIgnoreCase) ? "settings.db" : "Content.db";
                string otherPath = Path.Combine(_dbFolder, other);
                if (File.Exists(otherPath))
                    cmbDbFile.Items.Add(new DbFileItem { Path = otherPath, Display = other });
                cmbDbFile.SelectedIndex = 0;
            }
        }

        private void CmbDbFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = cmbDbFile.SelectedItem as DbFileItem;
            if (item == null) return;
            _currentDbPath = item.Path;
            LoadTableList();
        }

        private void LoadTableList()
        {
            _tableItems.Clear();
            lstTables.ItemsSource = null;
            txtTableCaption.Text = "选择左侧表以预览数据";
            dgData.ItemsSource = null;
            _currentTable = null;
            _currentTableName = null;

            if (string.IsNullOrEmpty(_currentDbPath) || !File.Exists(_currentDbPath))
            {
                txtStatus.Text = "请先打开数据库文件。";
                return;
            }

            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection("Data Source=" + _currentDbPath + ";Version=3;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string name = r.GetString(0);
                                _tableItems.Add(new TableItem { Name = name, DisplayName = name, IsTable = true });
                            }
                        }
                    }
                }

                if (Directory.Exists(_luaFolder))
                {
                    _tableItems.Add(new TableItem { Name = "__lua__", DisplayName = "(Lua 脚本)", IsTable = false });
                }

                lstTables.ItemsSource = _tableItems;
                txtStatus.Text = string.Format("已加载 {0} 个表。", _tableItems.Count(t => t.IsTable));
            }
            catch (Exception ex)
            {
                txtStatus.Text = "加载表列表失败: " + ex.Message;
            }
        }

        private void LstTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = lstTables.SelectedItem as TableItem;
            if (item == null) return;

            if (!item.IsTable)
            {
                if (item.Name == "__lua__")
                    ShowLuaList();
                return;
            }

            txtLuaHint.Visibility = Visibility.Collapsed;
            _currentTableName = item.Name;
            LoadTableData(_currentTableName);
        }

        private void LoadTableData(string tableName)
        {
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection("Data Source=" + _currentDbPath + ";Version=3;"))
                {
                    conn.Open();
                    var adapter = new System.Data.SQLite.SQLiteDataAdapter("SELECT * FROM [" + tableName + "]", conn);
                    _currentTable = new DataTable(tableName);
                    adapter.Fill(_currentTable);
                    if (_currentTable.PrimaryKey == null || _currentTable.PrimaryKey.Length == 0)
                    {
                        if (_currentTable.Columns.Contains("Id"))
                            _currentTable.PrimaryKey = new[] { _currentTable.Columns["Id"] };
                        else if (_currentTable.Columns.Count > 0)
                            _currentTable.PrimaryKey = new[] { _currentTable.Columns[0] };
                    }
                }

                dgData.ItemsSource = _currentTable.DefaultView;
                txtTableCaption.Text = string.Format("表: {0}（共 {1} 行）", tableName, _currentTable.Rows.Count);
                txtStatus.Text = string.Format("已加载表 {0}，共 {1} 行。可编辑后点击「保存修改」。", tableName, _currentTable.Rows.Count);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "加载表数据失败: " + ex.Message;
                dgData.ItemsSource = null;
            }
        }

        private void ShowLuaList()
        {
            dgData.ItemsSource = null;
            txtTableCaption.Text = "Data/lua 脚本（Aurora 分类/Quick View）";
            txtLuaHint.Visibility = Visibility.Visible;
            txtLuaHint.Text = "下方列出 Data/lua 下的 .lua 脚本，可与 settings.db 的 Quick Views 配合使用。";

            var luaFiles = Directory.Exists(_luaFolder)
                ? Directory.GetFiles(_luaFolder, "*.lua").Select(Path.GetFileName).OrderBy(x => x).ToList()
                : new List<string>();
            var dt = new DataTable();
            dt.Columns.Add("Lua 脚本", typeof(string));
            foreach (var f in luaFiles)
                dt.Rows.Add(f);
            dgData.ItemsSource = dt.DefaultView;
            _currentTable = null;
            _currentTableName = null;
            txtStatus.Text = string.Format("共 {0} 个 Lua 脚本。", luaFiles.Count);
        }

        private void DgData_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (_currentTable == null) return;
            if (e.Row.Item is DataRowView rowView && rowView.Row.RowState == DataRowState.Modified)
                e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 255, 200));
        }

        private void DgData_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) { }
        private void DgData_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e) { }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentTableName))
                LoadTableData(_currentTableName);
            else
                LoadTableList();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null || string.IsNullOrEmpty(_currentTableName))
            {
                MessageBox.Show("请先选择要保存的表。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_currentTable.GetChanges() == null)
            {
                MessageBox.Show("没有未保存的修改。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection("Data Source=" + _currentDbPath + ";Version=3;"))
                {
                    conn.Open();
                    var adapter = new System.Data.SQLite.SQLiteDataAdapter("SELECT * FROM [" + _currentTableName + "]", conn);
                    var builder = new System.Data.SQLite.SQLiteCommandBuilder(adapter);
                    adapter.UpdateCommand = builder.GetUpdateCommand();
                    adapter.InsertCommand = builder.GetInsertCommand();
                    adapter.DeleteCommand = builder.GetDeleteCommand();
                    int n = adapter.Update(_currentTable);
                    _currentTable.AcceptChanges();
                }
                txtStatus.Text = "保存成功。";
                MessageBox.Show("修改已保存。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "保存失败: " + ex.Message;
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private class TableItem
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public bool IsTable { get; set; }
        }

        private class DbFileItem
        {
            public string Path { get; set; }
            public string Display { get; set; }
            public override string ToString() => Display;
        }
    }
}
