using System.Windows.Controls;

namespace XTR_Toolbox.Dialogs
{
    public partial class StartupCreateUc
    {
        public StartupCreateUc()
        {
            InitializeComponent();
        }

        private void TbAutoPath_TextChanged(object sender, TextChangedEventArgs e) =>
            BtnCreate.IsEnabled = ((TextBox) sender).Text.Trim().Length != 0;
    }
}