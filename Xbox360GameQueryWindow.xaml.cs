using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion
{
    public partial class Xbox360GameQueryWindow : Window
    {
        private Xbox360TitlesDatabase _database;
        private List<GameInfo> _allGames;

        public Xbox360GameQueryWindow()
        {
            InitializeComponent();
            var icon = App.GetWindowIcon();
            if (icon != null) Icon = icon;
            _database = new Xbox360TitlesDatabase();
            _database.Initialize();
            _allGames = _database.GetAllGames();
            LoadCategoryFilter();
            RefreshList();
        }

        private void LoadCategoryFilter()
        {
            var categories = _database.GetCategories();
            cmbCategory.Items.Clear();
            cmbCategory.Items.Add("全部");
            foreach (var c in categories)
                cmbCategory.Items.Add(c);
            cmbCategory.SelectedIndex = 0;
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            cmbCategory.SelectedIndex = 0;
            RefreshList();
        }

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshList();
        }

        private void RefreshList()
        {
            var q = txtSearch?.Text?.Trim() ?? string.Empty;
            var list = string.IsNullOrEmpty(q) ? _allGames : _database.SearchGames(q);
            var selCat = cmbCategory?.SelectedItem as string;
            if (!string.IsNullOrEmpty(selCat) && selCat != "全部")
                list = list.Where(g => !string.IsNullOrEmpty(g.Category) && g.Category.Split(',').Select(s => s.Trim()).Contains(selCat, StringComparer.OrdinalIgnoreCase)).ToList();
            dgGames.ItemsSource = list;
            UpdateGameCount();
        }

        private void UpdateGameCount()
        {
            int count = 0;
            if (dgGames?.ItemsSource is IEnumerable<GameInfo> en)
                count = en.Count();
            txtGameCount.Text = $"共 {count} 个游戏";
        }

        private void DgGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            spGameDetails.Children.Clear();
            if (dgGames.SelectedItem is GameInfo g)
            {
                spGameDetails.Children.Add(new TextBlock { Text = $"游戏ID: {g.GameId}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
                spGameDetails.Children.Add(new TextBlock { Text = $"游戏名称: {g.Title ?? ""}", Margin = new Thickness(0, 0, 0, 6) });
                spGameDetails.Children.Add(new TextBlock { Text = $"中文名称: {g.ChineseTitle ?? ""}", Margin = new Thickness(0, 0, 0, 6) });
                if (!string.IsNullOrEmpty(g.Category))
                    spGameDetails.Children.Add(new TextBlock { Text = $"分类: {g.Category}", Margin = new Thickness(0, 0, 0, 6) });
                if (!string.IsNullOrEmpty(g.Synopsis))
                {
                    spGameDetails.Children.Add(new TextBlock { Text = "简介:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) });
                    var syn = new TextBlock { Text = g.Synopsis, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
                    spGameDetails.Children.Add(syn);
                }
            }
        }
    }
}
