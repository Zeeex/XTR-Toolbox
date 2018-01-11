using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

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
            BtnSave.IsEnabled = false;
            if (Equals(sender, BtnReset))
            {
                string[] lines = TbHostsFile.Text.Split('\n');
                List<string> comments = lines.Where(line => line.TrimStart(' ').StartsWith("#")).ToList();
                TbHostsFile.TextChanged -= TbHostsFile_TextChanged;
                TbHostsFile.Text = string.Join("\n", comments);
                TbHostsFile.TextChanged += TbHostsFile_TextChanged;
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

        private void TbHostsFile_TextChanged(object sender, EventArgs e) => BtnSave.IsEnabled = true;

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            StackPanelBtns.IsEnabled = false;
            using (HttpClient client = new HttpClient())
            {
                string result = await client.GetStringAsync("http://winhelp2002.mvps.org/hosts.txt");
                TbHostsFile.TextChanged -= TbHostsFile_TextChanged;
                TbHostsFile.Text = result;
                TbHostsFile.TextChanged += TbHostsFile_TextChanged;
                BtnHostsManage_Click(BtnSave, null);
            }

            StackPanelBtns.IsEnabled = true;
        }
    }
}