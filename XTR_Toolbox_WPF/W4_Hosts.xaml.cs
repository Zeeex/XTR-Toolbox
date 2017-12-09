using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace XTR_Toolbox
{
    public partial class Window4
    {
        private readonly string _hostsFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers/etc/hosts");

        public Window4()
        {
            InitializeComponent();
            HostsLoad();
        }

        private void BtnHostsReset_Click(object sender, RoutedEventArgs e)
        {
            FileStream fileStream = new FileStream(_hostsFilePath, FileMode.Create);
            RtbHostsFile.Document.Blocks.Clear();
            TextRange range = new TextRange(RtbHostsFile.Document.ContentStart, RtbHostsFile.Document.ContentEnd);
            range.Save(fileStream, DataFormats.Text);
        }

        private void BtnHostsSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileStream fileStream = new FileStream(_hostsFilePath, FileMode.Create);
                TextRange range = new TextRange(RtbHostsFile.Document.ContentStart, RtbHostsFile.Document.ContentEnd);
                range.Save(fileStream, DataFormats.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RtbHostsFile.Foreground = Brushes.White;
            RtbHostsFile.Background = Brushes.Black;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            RtbHostsFile.Foreground = Brushes.Black;
            RtbHostsFile.Background = Brushes.White;
        }

        private void HostsLoad()
        {
            try
            {
                if (!File.Exists(_hostsFilePath)) throw new Exception();
                RtbHostsFile.TextChanged -= RtbHostsFile_TextChanged;
                FileStream fileStream = new FileStream(_hostsFilePath, FileMode.Open, FileAccess.Read);
                TextRange range = new TextRange(RtbHostsFile.Document.ContentStart, RtbHostsFile.Document.ContentEnd);
                range.Load(fileStream, DataFormats.Text);
                RtbHostsFile.TextChanged += RtbHostsFile_TextChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Opacity = 0;
                Loaded += (s, e) => Close();
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void RtbHostsFile_TextChanged(object sender, EventArgs e) => BtnHostsSave.IsEnabled = true;
    }
}