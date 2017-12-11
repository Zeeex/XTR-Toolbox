using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
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

        private void BtnHostsManage_Click(object sender, RoutedEventArgs e)
        {
            if (Equals(sender, BtnHostsReset))
            {
                string[] lines = TbHostsFile.Text.Split('\n');
                List<string> comments = lines.Where(line => line.TrimStart(' ').StartsWith("#")).ToList();
                TbHostsFile.Text = string.Join("\n", comments);
                BtnHostsSave.IsEnabled = false;
            }
            try
            {
                using (StreamWriter file = File.CreateText(_hostsFilePath))
                {
                    file.Write(TbHostsFile.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            TbHostsFile.Foreground = Brushes.White;
            TbHostsFile.Background = Brushes.Black;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            TbHostsFile.Foreground = Brushes.Black;
            TbHostsFile.Background = Brushes.White;
        }

        private void HostsLoad()
        {
            try
            {
                if (!File.Exists(_hostsFilePath)) throw new Exception();
                TbHostsFile.TextChanged -= TbHostsFile_TextChanged;
                TbHostsFile.Text = File.ReadAllText(_hostsFilePath);
                TbHostsFile.TextChanged += TbHostsFile_TextChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Opacity = 0;
                Loaded += (s, e) => Close();
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void TbHostsFile_TextChanged(object sender, EventArgs e) => BtnHostsSave.IsEnabled = true;
    }
}