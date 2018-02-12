using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace XTR_Toolbox
{
    public partial class Window6
    {
        private readonly ObservableCollection<SoftwareModel> _softwareList = new ObservableCollection<SoftwareModel>();
        private SortAdorner _listViewSortAdorner;

        private GridViewColumnHeader _listViewSortCol;

        public Window6()
        {
            InitializeComponent();
            PopulateSoftware();
        }


        private static string FormatSize(string regSize)
        {
            float.TryParse(regSize, out float regSizeNum);
            return regSize.Length > 6
                ? (regSizeNum / 1024 / 1024).ToString("0.0") + " GB"
                : regSize.Length > 3
                    ? (regSizeNum / 1024).ToString("0.00") + " " +
                      "MB" +
                      "" +
                      ""
                    : regSize.Length != 0
                        ? regSize + " KB"
                        : "";
        }

        private static string GetMsiIconPath(string guid)
        {
            int startIndex = guid.IndexOf('{') + 1;
            guid = guid.Substring(startIndex, guid.IndexOf('}') - startIndex).Replace("-", "");
            int[] pattern = {8, 4, 4, 2, 2, 2, 2, 2, 2, 2, 2};
            StringBuilder returnString = new StringBuilder();
            int index = 0;
            foreach (int length in pattern)
            {
                returnString.Append(guid.Substring(index, length).Reverse().ToArray());
                index += length;
            }

            string decodedGuid = returnString.ToString();
            string icoPath =
                Registry.ClassesRoot.OpenSubKey(@"Installer\Products\" + decodedGuid)?.GetValue("ProductIcon", "")
                    .ToString() ?? Registry.CurrentUser
                    .OpenSubKey(@"Software\Microsoft\Installer\Products\" + decodedGuid)
                    ?.GetValue("ProductIcon", "").ToString();
            return File.Exists(icoPath) ? icoPath : null;
        }

        private static string GetVersion(RegistryKey subKey, string regName)
        {
            if ("0123456789".Contains(regName.ElementAt(regName.Length - 1))) return "";
            string regVer = subKey.GetValue("DisplayVersion", "").ToString();
            return regName.Contains(regVer) ? "" : regVer;
        }

        private static string ParseDate(string regDate, string regRemv)
        {
            DateTime.TryParseExact(regDate, "yyyyMMdd", null, DateTimeStyles.None, out DateTime dd);
            string date;
            if (dd.Date.Ticks != 0)
            {
                date = dd.ToString("yyyy-MM-dd");
            }
            else
            {
                regRemv = regRemv.Substring(0, regRemv.LastIndexOf("\\", StringComparison.Ordinal));
                date = Directory.Exists(regRemv) ? Directory.GetLastAccessTime(regRemv).ToString("yyyy-MM-dd") : "";
            }

            return date;
        }

        private static bool Uninstall(string uninstallPath, bool isMsi)
        {
            int separatorIndex = uninstallPath.IndexOf(".exe", StringComparison.Ordinal);
            if (separatorIndex < 1)
                return false;
            string arg = uninstallPath.Substring(separatorIndex + 4);
            uninstallPath = uninstallPath.Substring(0, separatorIndex + 4);
            if (isMsi)
                arg = arg.Replace("/I", "/X");
            if (!File.Exists(uninstallPath) && !isMsi) return true;
            int exitCode = Shared.StartProc(uninstallPath, arg);
            return (!isMsi || exitCode == 0) && !File.Exists(uninstallPath);
        }

        private void GetRegItems(RegistryKey key)
        {
            if (key == null) return;
            foreach (string subSoftware in key.GetSubKeyNames())
                try
                {
                    using (RegistryKey subKey = key.OpenSubKey(subSoftware))
                    {
                        if (subKey == null) continue;
                        string regName = subKey.GetValue("DisplayName", "").ToString();
                        string regRemv = subKey.GetValue("UninstallString", "").ToString().Replace("\"", "");
                        if (regName.Length == 0 || regRemv.Length == 0) continue;
                        string regLoca = subKey.GetValue("InstallLocation", "").ToString();
                        string regDate = subKey.GetValue("InstallDate", "").ToString();
                        string regSize = subKey.GetValue("EstimatedSize", "").ToString();
                        string regIcon = subKey.GetValue("DisplayIcon", "").ToString().Replace("\"", "");
                        BitmapSource ico = Shared.PathToIcon(regIcon);
                        bool isMsi = regRemv.Contains("MsiExec");
                        if (ico == null && isMsi)
                            ico = Shared.PathToIcon(GetMsiIconPath(regRemv));
                        _softwareList.Add(new SoftwareModel
                        {
                            Name = regName + "  " + GetVersion(subKey, regName),
                            Size = FormatSize(regSize),
                            DateInstalled = ParseDate(regDate, regRemv),
                            Uninstall = regRemv,
                            Icon = ico,
                            Msi = isMsi,
                            Location = regLoca
                        });
                    }
                }
                catch
                {
                    //ignored
                }

            key.Close();
        }

        private void LocationCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LvSoftware.SelectedItems.Cast<SoftwareModel>()
                .All(item => !string.IsNullOrEmpty(item.Location));
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

        private void ClipboardCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            int maxNameLength = (from SoftwareModel t in LvSoftware.SelectedItems select t.Name.Length)
                .Concat(new[] {0}).Max();
            foreach (SoftwareModel t in LvSoftware.SelectedItems)
            {
                string format = "{0,-" + maxNameLength + "}  |{1,12}  |{2,11}";
                sb.AppendFormat(format, t.Name, t.DateInstalled, t.Size);
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
        }

        private void PopulateSoftware()
        {
            const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            try
            {
                GetRegItems(Registry.LocalMachine.OpenSubKey(uninstallKey));
                GetRegItems(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(uninstallKey));
            }
            catch (SecurityException ex)
            {
                MessageBox.Show("No permission to read registry. Error: " + ex.Message);
            }
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(LvSoftware.ItemsSource).Refresh();
        }

        private bool UserFilter(object item)
        {
            if (TbSearch.Text.Length == 0) return true;
            return ((SoftwareModel) item).Name.IndexOf(TbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MsiCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LvSoftware.SelectedItems.Cast<SoftwareModel>().All(item => item.Msi);
        }

        private void MsiCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            for (int index = LvSoftware.SelectedItems.Count - 1; index >= 0; index--)
            {
                SoftwareModel item = (SoftwareModel) LvSoftware.SelectedItems[index];
                string msiExec = item.Uninstall;
                int separatorIndex = msiExec.IndexOf(".exe", StringComparison.Ordinal);
                if (separatorIndex < 1)
                    return;
                string arg = msiExec.Substring(separatorIndex + 4);
                msiExec = msiExec.Substring(0, separatorIndex + 4);
                switch (e.Parameter)
                {
                    case "Change":
                        arg = arg.Replace("/X", "/I");
                        Shared.StartProc(msiExec, arg);
                        break;
                    case "Repair":
                        arg = arg.Replace("/X", "/I").Replace("/I", "/F ");
                        Shared.StartProc(msiExec, arg);
                        break;
                }
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void OpenDirCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                foreach (SoftwareModel item in LvSoftware.SelectedItems) Shared.StartProc(item.Location, wait: false);
            }
            catch
            {
                //ignored
            }
        }

        private void UninstallCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedUICommand eCommand)) return;
            bool cmdForce = eCommand == SoftwareCmd.Force;
            bool isForce = false;
            if (cmdForce)
            {
                List<string> dirsRemove =
                    LvSoftware.SelectedItems.Cast<SoftwareModel>().Select(it => it.Location).ToList();
                MessageBoxResult ques = MessageBox.Show(
                    "These directories will be removed:\n\n" +
                    string.Join("\n", dirsRemove) +
                    "\n\nAre you sure you want to do this?",
                    "Remove application directory", MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.Yes);
                if (ques == MessageBoxResult.Yes) isForce = true;
                else return;
            }

            for (int index = LvSoftware.SelectedItems.Count - 1; index >= 0; index--)
            {
                SoftwareModel item = (SoftwareModel) LvSoftware.SelectedItems[index];
                if (!Uninstall(item.Uninstall, item.Msi)) continue;
                if (isForce)
                    try
                    {
                        Directory.Delete(item.Location, true);
                    }
                    catch
                    {
                        //ignored
                    }

                _softwareList.Remove(item);
            }
        }

        private class SoftwareModel
        {
            public string DateInstalled { [UsedImplicitly] get; set; }
            public BitmapSource Icon { [UsedImplicitly] get; set; }
            public string Location { [UsedImplicitly] get; set; }
            public bool Msi { [UsedImplicitly] get; set; }
            public string Name { [UsedImplicitly] get; set; }
            public string Size { [UsedImplicitly] get; set; }
            public string Uninstall { [UsedImplicitly] get; set; }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = new SoftwareModel();
            LvSoftware.ItemsSource = _softwareList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvSoftware.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("DateInstalled", ListSortDirection.Descending));
            view.Filter = UserFilter;
            TbSearch.Focus();
            Shared.SnackBarTip(MainSnackbar);
        }
    }

    public static class SoftwareCmd
    {
        public static readonly RoutedUICommand Clip = new RoutedUICommand("Copy to Clipboard", "Clip",
            typeof(SoftwareCmd),
            new InputGestureCollection {new KeyGesture(Key.C, ModifierKeys.Control)});

        public static readonly RoutedUICommand Dir = new RoutedUICommand("Open Directory", "Dir", typeof(SoftwareCmd),
            new InputGestureCollection {new KeyGesture(Key.O, ModifierKeys.Control)});

        public static readonly RoutedUICommand Force = new RoutedUICommand("Force Remove", "Force", typeof(SoftwareCmd),
            new InputGestureCollection {new KeyGesture(Key.F, ModifierKeys.Control)});

        public static readonly RoutedUICommand Msi = new RoutedUICommand();

        public static readonly RoutedUICommand Remove = new RoutedUICommand("Uninstall", "Rem", typeof(SoftwareCmd),
            new InputGestureCollection {new KeyGesture(Key.R, ModifierKeys.Control)});
    }
}