using System.Windows;

namespace U_Wii_X_Fusion
{
    public partial class Xbox360AddToDbDialog : Window
    {
        public string TitleId => txtTitleId?.Text?.Trim() ?? "";
        public string GameName => txtName?.Text?.Trim() ?? "";
        public string ChineseName => txtChineseName?.Text?.Trim() ?? "";

        public Xbox360AddToDbDialog(string titleId, string name, string chineseName)
        {
            InitializeComponent();
            txtTitleId.Text = titleId ?? "";
            txtName.Text = name ?? "";
            txtChineseName.Text = chineseName ?? "";
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitleId.Text))
            {
                MessageBox.Show("请输入游戏ID（Title ID）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
