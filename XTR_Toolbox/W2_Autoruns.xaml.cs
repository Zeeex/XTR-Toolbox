using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    public partial class Window2
    {
        private readonly ObservableCollection<StartupModel> _autorunsList = new ObservableCollection<StartupModel>();
        private readonly HashSet<string> _brokenRun = new HashSet<string>();

        private readonly string[] _groupName =
            {"All Users", "Current User", "All Users (x64)", "Invalid (Broken)"};

        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window2()
        {
            InitializeComponent();
            AutorunsScan();
            DataContext = new StartupModel();
            LvAutoruns.ItemsSource = _autorunsList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvAutoruns.ItemsSource);
            PropertyGroupDescription groupBind = new PropertyGroupDescription("Group");
            view.GroupDescriptions.Add(groupBind);
            Shared.SnackBarTip(MainSnackbar);
        }

        private void AddBroken(IReadOnlyCollection<string> brokenItems, int groupNum)
        {
            if (brokenItems.Count <= 0) return;
            List<string> output = brokenItems.ToList();
            foreach (string outputItem in output)
                _autorunsList.Add(new StartupModel
                {
                    Name = outputItem,
                    Path = "",
                    Enabled = "",
                    Group = _groupName[groupNum]
                });
        }

        private void AddHklm(ref int groupNum, RegistryKey reg64)
        {
            RegistryKey keyInfo = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            RegistryKey runInfo64 =
                reg64.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32");
            if (keyInfo != null && runInfo64 != null)
                GetEntries(keyInfo, runInfo64, _groupName[groupNum++]);
        }

        private void AddHkcu(ref int groupNum)
        {
            RegistryKey keyInfo = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            RegistryKey runInfo =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            if (keyInfo != null && runInfo != null)
                GetEntries(keyInfo, runInfo, _groupName[groupNum++]);
        }

        private void AddHkcu64(ref int groupNum, RegistryKey reg64)
        {
            RegistryKey keyInfo64 = reg64.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            RegistryKey runInfo64 =
                reg64.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            if (keyInfo64 != null && runInfo64 != null)
                GetEntries(keyInfo64, runInfo64, _groupName[groupNum++]);
        }

        private void AutorunsScan()
        {
            int groupNum = 0;
            RegistryKey reg64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            AddHklm(ref groupNum, reg64);
            AddHkcu(ref groupNum);
            AddHkcu64(ref groupNum, reg64);
            AddBroken(_brokenRun, groupNum);
        }

        private void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBoxResult mB = MessageBox.Show("Are you sure you want to delete selected item(s)?",
                "Delete Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (mB == MessageBoxResult.No) return;
            const string startKeyString = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string[] runKeys =
            {
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
            };
            try
            {
                using (RegistryKey reg64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    for (int index = LvAutoruns.SelectedItems.Count - 1; index >= 0; index--)
                    {
                        StartupModel itemToDelete = (StartupModel) LvAutoruns.SelectedItems[index];
                        string nameAutorun = itemToDelete?.Name;
                        if (string.IsNullOrEmpty(nameAutorun)) continue;
                        string itemGroup = itemToDelete.Group;
                        _autorunsList.Remove(itemToDelete);
                        RegistryKey startupKey, runKey;
                        if (Equals(itemGroup, _groupName[0]))
                        {
                            startupKey = Registry.LocalMachine.OpenSubKey(startKeyString, true);
                            runKey = reg64.OpenSubKey(runKeys[0], true);
                        }
                        else if (Equals(itemGroup, _groupName[1]))
                        {
                            startupKey = Registry.CurrentUser.OpenSubKey(startKeyString, true);
                            runKey = Registry.CurrentUser.OpenSubKey(runKeys[1], true);
                        }
                        else if (Equals(itemGroup, _groupName[2]))
                        {
                            startupKey = reg64.OpenSubKey(startKeyString, true);
                            runKey = reg64.OpenSubKey(runKeys[1], true);
                        }
                        // INVALID LIST
                        else if (Equals(itemGroup, _groupName[_groupName.Length - 1]))
                        {
                            foreach (string sK in runKeys)
                                using (RegistryKey keyVoid = reg64.OpenSubKey(sK, true))
                                {
                                    keyVoid?.DeleteValue(nameAutorun, false);
                                }

                            using (RegistryKey keyVoid1 = Registry.CurrentUser.OpenSubKey(runKeys[1], true))
                            {
                                keyVoid1?.DeleteValue(nameAutorun, false);
                            }

                            continue;
                        }
                        else
                        {
                            continue;
                        }

                        // DELETING VALID ENTRY
                        startupKey?.DeleteValue(nameAutorun, false);
                        runKey?.DeleteValue(nameAutorun, false);
                        startupKey?.Close();
                        runKey?.Close();
                    }
                }
            }
            catch
            {
                //ignored
            }
        }

        private void GetEntries(RegistryKey entryKey, RegistryKey runKey, string groupName)
        {
            try
            {
                foreach (string pathName in entryKey.GetValueNames())
                foreach (string runName in runKey.GetValueNames())
                {
                    if (runKey.GetValue(runName).GetType().Name != "Byte[]") continue;
                    if (pathName == runName)
                    {
                        byte[] runValueBytes = (byte[]) runKey.GetValue(runName);
                        string cleanValue = entryKey
                            .GetValue(pathName, "", RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                        // REMOVE QUOTES AND PARAMETERS FROM NAMES =======
                        if (cleanValue.IndexOf(".exe", StringComparison.Ordinal) != -1)
                            cleanValue =
                                cleanValue.Substring(0, cleanValue.IndexOf(".exe", StringComparison.Ordinal) + 4)
                                    .Replace("\"", "");
                        // IMPROVE...
                        if (cleanValue.LastIndexOf("%ProgramFiles%", StringComparison.Ordinal) != -1 &&
                            Environment.Is64BitOperatingSystem)
                        {
                            cleanValue =
                                cleanValue.Substring(cleanValue.LastIndexOf("%", StringComparison.Ordinal) + 1);
                            cleanValue =
                                Path.GetFullPath(Environment.GetEnvironmentVariable("ProgramW6432") + cleanValue);
                        }

                        string fileExe = Environment.ExpandEnvironmentVariables(cleanValue);
                        _autorunsList.Add(new StartupModel
                        {
                            Name = runName,
                            Icon = Shared.PathToIcon(fileExe),
                            Path = fileExe,
                            Enabled = runValueBytes[0] == 02 ? "Yes" : "No",
                            Group = groupName
                        });
                    }
                    else
                    {
                        if (!entryKey.GetValueNames().Contains(runName))
                            _brokenRun.Add(runName);
                    }
                }

                entryKey.Close();
                runKey.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LvUsersColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = sender as GridViewColumnHeader;
            string sortBy = column?.Tag.ToString();
            if (_listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Remove(_listViewSortAdorner);
                LvAutoruns.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (Equals(_listViewSortCol, column) && Equals(_listViewSortAdorner.Direction, newDir))
                newDir = ListSortDirection.Descending;

            _listViewSortCol = column;
            _listViewSortAdorner = new SortAdorner(_listViewSortCol, newDir);
            if (_listViewSortCol != null)
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Add(_listViewSortAdorner);
            if (sortBy != null)
                LvAutoruns.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void OpenDirCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                foreach (StartupModel item in LvAutoruns.SelectedItems)
                {
                    string dirFull = Path.GetDirectoryName(item.Path);
                    if (dirFull != null)
                        Shared.StartProc(dirFull, wait: false);
                }
            }
            catch
            {
                //ignored
            }
        }

        private void StateChangeCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedUICommand eCommand)) return;
            bool enb = eCommand == StartupCmd.Enable;
            byte[] itemState = enb
                ? new byte[] {0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}
                : new byte[] {0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            string[] runKeyList =
            {
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
            };
            using (RegistryKey reg64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                foreach (StartupModel itemToSet in LvAutoruns.SelectedItems)
                {
                    if (itemToSet == null) continue;
                    itemToSet.Enabled = enb ? "Yes" : "No";
                    RegistryKey startupKey;
                    if (Equals(itemToSet.Group, _groupName[0]))
                        startupKey = reg64.OpenSubKey(runKeyList[0], true);
                    else if (Equals(itemToSet.Group, _groupName[1]))
                        startupKey = Registry.CurrentUser.OpenSubKey(runKeyList[1], true);
                    else if (Equals(itemToSet.Group, _groupName[2]))
                        startupKey = reg64.OpenSubKey(runKeyList[1], true);
                    else
                        continue;
                    startupKey?.SetValue(itemToSet.Name, itemState, RegistryValueKind.Binary);
                    startupKey?.Close();
                }
            }
        }

        private void ValidItemCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LvAutoruns.SelectedItems.Cast<StartupModel>()
                .All(item => item.Group != _groupName[_groupName.Length - 1]);
        }

        private class StartupModel : INotifyPropertyChanged
        {
            private string _enabled;

            public event PropertyChangedEventHandler PropertyChanged;

            public string Enabled
            {
                [UsedImplicitly] get => _enabled;
                set
                {
                    if (_enabled == value) return;
                    _enabled = value;
                    NotifyPropertyChanged(nameof(Enabled));
                }
            }

            public string Group { [UsedImplicitly] get; set; }
            public BitmapSource Icon { [UsedImplicitly] get; set; }
            public string Name { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private void ClipboardCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            int maxNameLength = (from StartupModel t in LvAutoruns.SelectedItems select t.Name.Length)
                .Concat(new[] {0}).Max();
            foreach (StartupModel t in LvAutoruns.SelectedItems)
            {
                string format = "{0,-" + maxNameLength + "}  | {1,-3} | {2}";
                sb.AppendFormat(format, t.Name, t.Enabled, t.Path);
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
        }
    }

    public static class StartupCmd
    {
        public static readonly RoutedUICommand Dir = new RoutedUICommand("Open Directory", "Dir", typeof(StartupCmd),
            new InputGestureCollection {new KeyGesture(Key.O, ModifierKeys.Control)});

        public static readonly RoutedUICommand Enable = new RoutedUICommand("Enable", "En", typeof(StartupCmd),
            new InputGestureCollection {new KeyGesture(Key.E, ModifierKeys.Control)});

        public static readonly RoutedUICommand Disable = new RoutedUICommand("Disable", "Dis", typeof(StartupCmd),
            new InputGestureCollection {new KeyGesture(Key.D, ModifierKeys.Control)});

        public static readonly RoutedUICommand Clip = new RoutedUICommand("Copy to Clipboard", "Clip",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.C, ModifierKeys.Control)});

        public static readonly RoutedUICommand Delete = new RoutedUICommand("Delete", "Del", typeof(StartupCmd),
            new InputGestureCollection {new KeyGesture(Key.Delete, ModifierKeys.Control)});
    }
}