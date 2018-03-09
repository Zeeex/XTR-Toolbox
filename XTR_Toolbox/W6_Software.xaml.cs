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
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using XTR_Toolbox.Classes;
using XTR_Toolbox.Dialogs;

namespace XTR_Toolbox
{
    public partial class Window6
    {
        private static readonly ObservableCollection<SoftwareLogModel> HistoryList =
            new ObservableCollection<SoftwareLogModel>();

        private static BitmapSource _msiIcon;
        private readonly IndicatorModel _indiBind = new IndicatorModel();

        private SortAdorner _listViewSortAdorner;

        private GridViewColumnHeader _listViewSortCol;
        private ObservableCollection<SoftwareModel> _softwareList;

        public Window6()
        {
            InitializeComponent();
        }

        private static double FormatKbToMb(string regSize)
        {
            float.TryParse(regSize, out float regSizeNum);
            return Math.Round(regSizeNum / 1024, 2);
        }

        private static BitmapSource GetIcon(string regUnin, string regIcon, out bool isMsi)
        {
            BitmapSource ico = Shared.PathToIcon(regIcon);
            isMsi = regUnin.Contains("MsiExec");
            if (ico != null) return ico;
            ico = isMsi
                ? MsiIconToUse(regUnin)
                : Shared.PathToIcon(regUnin.Substring(0, regUnin.IndexOf(".exe", StringComparison.Ordinal) + 4));
            return ico;
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

        private static void GetSoftware(ICollection<SoftwareModel> tList)
        {
            const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            RegistryKey[] keys =
            {
                Registry.LocalMachine.OpenSubKey(uninstallKey),
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(uninstallKey),
                Registry.CurrentUser.OpenSubKey(uninstallKey)
            };
            try
            {
                foreach (RegistryKey key in keys)
                {
                    foreach (string subSoftware in key.GetSubKeyNames())
                        try
                        {
                            using (RegistryKey subKey = key.OpenSubKey(subSoftware))
                            {
                                if (subKey == null) continue;
                                string regName = subKey.GetValue("DisplayName", "").ToString();
                                string regUnin = subKey.GetValue("UninstallString", "").ToString().Replace("\"", "");
                                if (regName.Length == 0 || regUnin.Length == 0) continue;
                                string regKey = subKey.ToString();
                                string regLoca = subKey.GetValue("InstallLocation", "").ToString();
                                string regDate = subKey.GetValue("InstallDate", "").ToString();
                                string regSize = subKey.GetValue("EstimatedSize", "").ToString();
                                string regIcon = subKey.GetValue("DisplayIcon", "").ToString().Replace("\"", "");
                                BitmapSource ico = GetIcon(regUnin, regIcon, out bool isMsi);
                                tList.Add(new SoftwareModel
                                {
                                    Name = regName + "  " + GetVersion(subKey, regName),
                                    Size = FormatKbToMb(regSize),
                                    DateInstalled = ParseDate(regDate, regUnin),
                                    Uninstall = regUnin,
                                    Icon = ico,
                                    IsMsi = isMsi,
                                    Location = regLoca,
                                    RegPath = regKey
                                });
                            }
                        }
                        catch
                        {
                            // ignored
                        }

                    key.Close();
                }
            }
            catch (SecurityException ex)
            {
                MessageBox.Show("No permission to read registry. Try to run as Administrator.\nError: " + ex.Message);
            }
        }

        private static string GetVersion(RegistryKey subKey, string regName)
        {
            if ("0123456789".Contains(regName.ElementAt(regName.Length - 1))) return "";
            string regVer = subKey.GetValue("DisplayVersion", "").ToString();
            return regName.Contains(regVer) ? "" : regVer;
        }

        private static BitmapSource MsiIconToUse(string regUnin)
        {
            BitmapSource msiIcon = Shared.PathToIcon(GetMsiIconPath(regUnin)) ?? _msiIcon;
            return msiIcon;
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

        private static ObservableCollection<SoftwareModel> PopulateSoftware()
        {
            try
            {
                _msiIcon = Shared.PathToIcon(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "msiexec.exe"));
            }
            catch
            {
                _msiIcon = null;
            }

            ICollection<SoftwareModel> tList = new List<SoftwareModel>();
            GetSoftware(tList);
            return new ObservableCollection<SoftwareModel>(tList);
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
            int exitCode = CustomProc.StartProc(uninstallPath, arg);
            return (!isMsi || exitCode == 0) && !File.Exists(uninstallPath);
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

        private void LocationCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LvSoftware.SelectedItems.Cast<SoftwareModel>()
                .All(item => !string.IsNullOrEmpty(item.Location));
        }

        private void LvSoftware_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListView) sender).SelectedItems.Count == 0)
            {
                _indiBind.SelectedSize = 0;
                return;
            }

            foreach (double s in e.AddedItems.Cast<SoftwareModel>().Select(it => it.Size))
                _indiBind.SelectedSize += s;

            foreach (double s in e.RemovedItems.Cast<SoftwareModel>().Select(it => it.Size))
                _indiBind.SelectedSize -= s;
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

        private void MsiCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LvSoftware.SelectedItems.Cast<SoftwareModel>().All(item => item.IsMsi);

        private async void MsiCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int selCount = LvSoftware.SelectedItems.Count;
            if (selCount > 20)
            {
                object result = await DialogHost.Show(
                    new GenYesNoUc($"Are you sure you want to work with {selCount} items?"), "RootDialog");
                if (result == null || result.ToString() != "Y") return;
            }

            for (int index = selCount - 1; index >= 0; index--)
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
                        CustomProc.StartProc(msiExec, arg);
                        break;
                    case "Repair":
                        arg = arg.Replace("/X", "/I").Replace("/I", "/F ");
                        CustomProc.StartProc(msiExec, arg);
                        break;
                }
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void OpenDirCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                foreach (SoftwareModel item in LvSoftware.SelectedItems)
                    CustomProc.StartProc(item.Location, wait: false);
            }
            catch
            {
                //ignored
            }
        }

        private void OpenRegCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                foreach (SoftwareModel item in LvSoftware.SelectedItems)
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit",
                        "LastKey",
                        item.RegPath);
                    // Need 64-bit Process v3.0
                    CustomProc.StartProc("regedit", "-m", wait: false);
                }
            }
            catch
            {
                // ignored
            }
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(LvSoftware.ItemsSource).Refresh();

        private async void UninstallCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedUICommand eCommand)) return;
            if (eCommand == SoftwareCmd.Force)
            {
                string[] dirsRemove = LvSoftware.SelectedItems.Cast<SoftwareModel>()
                    .Where(it => !string.IsNullOrEmpty(it.Location)).Select(it => it.Location).ToArray();
                IEnumerable<string> regsRemove = LvSoftware.SelectedItems.Cast<SoftwareModel>()
                    .Select(it => it.RegPath);
                new ForceRemoveDialog(dirsRemove, regsRemove) {Owner = this}.ShowDialog();
                if (!ForceRemoveDialog.ForceOk) return;
            }

            int selCount = LvSoftware.SelectedItems.Count;
            if (selCount > 20)
            {
                object result = await DialogHost.Show(
                    new GenYesNoUc($"Are you sure you want to uninstall {selCount} items?"), "RootDialog");
                if (result == null || result.ToString() != "Y") return;
            }

            for (int index = selCount - 1; index >= 0; index--)
            {
                SoftwareModel item = (SoftwareModel) LvSoftware.SelectedItems[index];
                if (!Uninstall(item.Uninstall, item.IsMsi)) continue;
                if (ForceRemoveDialog.ForceOk)
                    try
                    {
                        string[] splitOnHive = item.RegPath.Split(new[] {'\\'}, 2);
                        RegistryHive regHive = Shared.StringToRegistryHive(splitOnHive[0]);
                        RegistryKey.OpenBaseKey(regHive, RegistryView.Registry32)
                            .DeleteSubKeyTree(splitOnHive[1], false);
                        RegistryKey.OpenBaseKey(regHive, RegistryView.Registry64)
                            .DeleteSubKeyTree(splitOnHive[1], false);
                        if (Directory.Exists(item.Location))
                            Directory.Delete(item.Location, true);
                    }
                    catch
                    {
                        //ignored
                    }

                _softwareList.Remove(item);
                HistoryList.Add(new SoftwareLogModel
                    {History = $"{DateTime.Now.ToShortTimeString()} - {item.Name} : Uninstalled"});
            }
        }

        private bool UserFilter(object item)
        {
            if (TbSearch.Text.Length == 0) return true;
            return ((SoftwareModel) item).Name.IndexOf(TbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            _softwareList = PopulateSoftware();
            LvSoftware.ItemsSource = _softwareList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvSoftware.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("DateInstalled", ListSortDirection.Descending));
            view.Filter = UserFilter;
            BtnSelSize.DataContext = _indiBind;
            TbSearch.Focus();
            LbHistory.ItemsSource = HistoryList;
            Shared.ShowSnackBar(MainSnackbar);
        }

        private class IndicatorModel : INotifyPropertyChanged
        {
            private double _selectedSize;

            public event PropertyChangedEventHandler PropertyChanged;

            public double SelectedSize
            {
                [UsedImplicitly] get => _selectedSize;

                set
                {
                    _selectedSize = _selectedSize < 0 ? 0 : Math.Round(value, 2);
                    NotifyPropertyChanged(nameof(SelectedSize));
                }
            }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private class SoftwareLogModel
        {
            public string History { [UsedImplicitly] get; set; }
        }

        private class SoftwareModel
        {
            public string DateInstalled { [UsedImplicitly] get; set; }
            public BitmapSource Icon { [UsedImplicitly] get; set; }
            public bool IsMsi { [UsedImplicitly] get; set; }
            public string Location { [UsedImplicitly] get; set; }
            public string Name { [UsedImplicitly] get; set; }
            public string RegPath { [UsedImplicitly] get; set; }
            public double Size { [UsedImplicitly] get; set; }
            public string Uninstall { [UsedImplicitly] get; set; }
        }
    }

    public static class SoftwareCmd
    {
        public static readonly RoutedUICommand Clip = new RoutedUICommand("Copy to Clipboard", "Clip",
            typeof(SoftwareCmd), new InputGestureCollection {new KeyGesture(Key.C, ModifierKeys.Control)});

        public static readonly RoutedUICommand Dir = new RoutedUICommand("Open Directory", "Dir",
            typeof(SoftwareCmd), new InputGestureCollection {new KeyGesture(Key.O, ModifierKeys.Control)});

        public static readonly RoutedUICommand Force = new RoutedUICommand("Force Remove...", "Force",
            typeof(SoftwareCmd), new InputGestureCollection {new KeyGesture(Key.F, ModifierKeys.Control)});

        public static readonly RoutedUICommand Msi = new RoutedUICommand();

        public static readonly RoutedUICommand RegJump = new RoutedUICommand("Open in Registry", "Reg",
            typeof(SoftwareCmd), new InputGestureCollection {new KeyGesture(Key.J, ModifierKeys.Control)});

        public static readonly RoutedUICommand Remove = new RoutedUICommand("Uninstall", "Rem",
            typeof(SoftwareCmd), new InputGestureCollection {new KeyGesture(Key.R, ModifierKeys.Control)});
    }
}