using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace XTR_Toolbox
{
    public partial class Window6
    {
        private readonly ObservableCollection<SoftwareItem> _softwareList = new ObservableCollection<SoftwareItem>();
        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window6()
        {
            InitializeComponent();
            PopulateSoftware();
            DataContext = new SoftwareItem();
            LvSoftware.ItemsSource = _softwareList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvSoftware.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("DateInstalled", ListSortDirection.Descending));
            view.Filter = UserFilter;
            TxtSearch.Focus();
        }

        private static bool ManageSoftware(string uninstallPath)
        {
            int separatorIndex = uninstallPath.IndexOf(".exe", StringComparison.Ordinal);
            if (separatorIndex < 1)
            {
                MessageBox.Show("Invalid uninstall entry");
                return false;
            }
            string arg = uninstallPath.Substring(separatorIndex + 4);
            uninstallPath = uninstallPath.Substring(0, separatorIndex + 4);
            bool msi = uninstallPath.StartsWith("MsiExec");
            if (msi)
                arg = arg.Replace("/I", "/X");
            if (!File.Exists(uninstallPath) && !msi) return true;
            int exitCode = StartProc(uninstallPath, arg);
            return (!msi || exitCode == 0) && !File.Exists(uninstallPath);
        }

        private static int StartProc(string uninstallPath, string arg)
        {
            using (Process proc = new Process())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = uninstallPath,
                    Arguments = arg
                };
                proc.StartInfo = startInfo;
                proc.Start();
                proc.WaitForExit();
                return proc.ExitCode;
            }
        }

        private void LvUsersColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = sender as GridViewColumnHeader;
            string sortBy = column?.Tag.ToString();
            if (_listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Remove(_listViewSortAdorner);
                LvSoftware.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (Equals(_listViewSortCol, column) && Equals(_listViewSortAdorner.Direction, newDir))
                newDir = ListSortDirection.Descending;

            _listViewSortCol = column;
            _listViewSortAdorner = new SortAdorner(_listViewSortCol, newDir);
            if (_listViewSortCol != null) AdornerLayer.GetAdornerLayer(_listViewSortCol).Add(_listViewSortAdorner);
            if (sortBy != null) LvSoftware.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            for (int index = LvSoftware.SelectedItems.Count - 1; index >= 0; index--)
            {
                SoftwareItem listItem = (SoftwareItem) LvSoftware.SelectedItems[index];
                if (Equals(sender, Uninstall) && ManageSoftware(listItem.Uninstall)) _softwareList.Remove(listItem);
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void PopulateSoftware()
        {
            const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            RegistryKey bitView64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            FillList(Registry.LocalMachine.OpenSubKey(uninstallKey));
            FillList(bitView64.OpenSubKey(uninstallKey));
        }

        private void FillList(RegistryKey key)
        {
            if (key == null) return;
            foreach (string subkeyName in key.GetSubKeyNames())
                using (RegistryKey subKey = key.OpenSubKey(subkeyName))
                {
                    if (subKey == null) continue;
                    try
                    {
                        string regName = subKey.GetValue("DisplayName").ToString();
                        string regDate = subKey.GetValue("InstallDate", "").ToString();
                        string regSize = subKey.GetValue("EstimatedSize", "").ToString();
                        string regUninstall = subKey.GetValue("UninstallString").ToString().Replace("\"", "");
                        DateTime.TryParseExact(regDate, "yyyyMMdd", null, DateTimeStyles.None, out DateTime dd);
                        float.TryParse(regSize, out float regSizeNum);
                        string regSizeStr = regSize.Length > 6
                            ? (regSizeNum / 1024 / 1024).ToString("0.0") + " GB"
                            : regSize.Length > 3
                                ? (regSizeNum / 1024).ToString("0.00") + " MB"
                                : regSize.Length != 0
                                    ? regSize + " KB"
                                    : "";
                        _softwareList.Add(new SoftwareItem
                        {
                            Name = regName + "  " + GetVersion(subKey, regName),
                            Size = regSizeStr,
                            DateInstalled = dd.Date.Ticks != 0 ? dd.ToString("yyyy-MM-dd") : "",
                            Uninstall = regUninstall,
                            Icon = GetIcon(subKey)
                        });
                    }
                    catch (NullReferenceException)
                    {
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
            key.Close();
        }


        private static string GetVersion(RegistryKey subKey, string regName)
        {
            if ("0123456789".Contains(regName.ElementAt(regName.Length - 1))) return "";
            string regVer = subKey.GetValue("DisplayVersion", "").ToString();
            return regName.Contains(regVer) ? "" : regVer;
        }

        private static BitmapSource GetIcon(RegistryKey subKey)
        {
            string regIcon = subKey.GetValue("DisplayIcon", "").ToString().Trim('\"');
            if (string.IsNullOrEmpty(regIcon))
                return null;
            if (!File.Exists(regIcon))
            {
                int commaIndex = regIcon.IndexOf("\"", StringComparison.Ordinal);
                if (commaIndex >= 0)
                    regIcon = regIcon.Substring(0, commaIndex);
            }
            if (!File.Exists(regIcon))
                return null;
            using (Icon sysicon = System.Drawing.Icon.ExtractAssociatedIcon(regIcon))
            {
                return sysicon == null
                    ? null
                    : Imaging.CreateBitmapSourceFromHIcon(sysicon.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(LvSoftware.ItemsSource).Refresh();
        }

        private bool UserFilter(object item)
        {
            if (string.IsNullOrEmpty(TxtSearch.Text))
                return true;
            return ((SoftwareItem) item).Name.IndexOf(TxtSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private class SoftwareItem
        {
            public string DateInstalled { [UsedImplicitly] get; set; }
            public string Name { [UsedImplicitly] get; set; }
            public string Size { [UsedImplicitly] get; set; }
            public BitmapSource Icon { [UsedImplicitly] get; set; }
            public string Uninstall { [UsedImplicitly] get; set; }
        }
    }
}