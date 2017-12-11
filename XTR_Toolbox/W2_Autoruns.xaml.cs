using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace XTR_Toolbox
{
    public partial class Window2
    {
        private static readonly ObservableCollection<RunItem> AutorunsList = new ObservableCollection<RunItem>();

        private static readonly string[] GroupName =
            {"All Users", "Current User", "All Users (x64)", "Invalid (Broken)"};

        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window2()
        {
            InitializeComponent();
            AutorunsList.Clear();
            AutorunsScan();
            DataContext = new RunItem();
            LvAutoruns.ItemsSource = AutorunsList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvAutoruns.ItemsSource);
            if (view.Groups?.Count.ToString() == null)
            {
                PropertyGroupDescription groupDescription = new PropertyGroupDescription("Group");
                view.GroupDescriptions.Add(groupDescription);
            }
        }

        private static void AutorunsScan()
        {
            int groupNum = 0;
            HashSet<string> brokenRun = new HashSet<string>();
            RegistryKey registryViewHklm64 =
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            // HKLM
            RegistryKey keyHklm32 = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            RegistryKey runKeyHklm32 =
                registryViewHklm64.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32");
            if (keyHklm32 != null && runKeyHklm32 != null)
                GetEntries(keyHklm32, runKeyHklm32, GroupName[groupNum++], brokenRun);
            // HKCU
            RegistryKey keyHkcu32 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            RegistryKey runKeyHkcu32 =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            if (keyHkcu32 != null && runKeyHkcu32 != null)
                GetEntries(keyHkcu32, runKeyHkcu32, GroupName[groupNum++], brokenRun);
            // HKLM *64!
            RegistryKey keyHklm64 = registryViewHklm64.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            RegistryKey runKeyHklm64 =
                registryViewHklm64.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
            if (keyHklm64 != null && runKeyHklm64 != null)
                GetEntries(keyHklm64, runKeyHklm64, GroupName[groupNum++], brokenRun);
            // BROKEN ENTRIES
            if (brokenRun.Count > 0)
            {
                List<string> output = brokenRun.ToList(); // .NET 4.0
                foreach (string outputItem in output)
                    AutorunsList.Add(new RunItem
                    {
                        Name = outputItem,
                        Path = "",
                        Enabled = "",
                        Group = GroupName[groupNum]
                    });
            }
        }

        private void BtnChangeState_Click(object sender, RoutedEventArgs e)
        {
            using (RegistryKey registryViewHklm64 =
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                byte[] itemStatusChange = Equals(sender, BtnDisable)
                    ? new byte[] {0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}
                    : new byte[] {0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                string[] runKeyList =
                {
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
                };
                foreach (RunItem itemToSet in LvAutoruns.SelectedItems)
                {
                    if (itemToSet == null) continue;
                    RegistryKey startupKey;
                    itemToSet.Enabled = Equals(sender, BtnDisable) ? "No" : "Yes";
                    if (Equals(itemToSet.Group, GroupName[0]))
                        startupKey = registryViewHklm64.OpenSubKey(runKeyList[0], true);
                    else if (Equals(itemToSet.Group, GroupName[1]))
                        startupKey = Registry.CurrentUser.OpenSubKey(runKeyList[1], true);
                    else if (Equals(itemToSet.Group, GroupName[2]))
                        startupKey = registryViewHklm64.OpenSubKey(runKeyList[1], true);
                    else continue;
                    string nameAutorun = itemToSet.Name;
                    if (!string.IsNullOrEmpty(nameAutorun))
                        startupKey?.SetValue(nameAutorun, itemStatusChange, RegistryValueKind.Binary);
                    startupKey?.Close();
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            RegistryKey registryViewHklm64 =
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            const string startKeyString = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string[] runKeyList =
            {
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
            };
            List<RunItem> collDelete = new List<RunItem>();
            foreach (RunItem itemtoDelete in LvAutoruns.SelectedItems)
                try
                {
                    if (itemtoDelete == null) continue;
                    RegistryKey startupKey, runKey;
                    string nameAutorun = itemtoDelete.Name;
                    if (string.IsNullOrEmpty(nameAutorun)) continue;
                    if (Equals(itemtoDelete?.Group, GroupName[0]))
                    {
                        startupKey = Registry.LocalMachine.OpenSubKey(startKeyString, true);
                        runKey = registryViewHklm64.OpenSubKey(runKeyList[0], true);
                    }
                    else if (Equals(itemtoDelete?.Group, GroupName[1]))
                    {
                        startupKey = Registry.CurrentUser.OpenSubKey(startKeyString, true);
                        runKey = Registry.CurrentUser.OpenSubKey(runKeyList[1], true);
                    }
                    else if (Equals(itemtoDelete?.Group, GroupName[2]))
                    {
                        startupKey = registryViewHklm64.OpenSubKey(startKeyString, true);
                        runKey = registryViewHklm64.OpenSubKey(runKeyList[1], true);
                    }
                    // INVALID LIST
                    else if (Equals(itemtoDelete?.Group, GroupName[GroupName.Length - 1]))
                    {
                        foreach (string singleRun in runKeyList)
                        {
                            RegistryKey runKeyInvalid = registryViewHklm64.OpenSubKey(singleRun, true);
                            runKeyInvalid?.DeleteValue(nameAutorun, false);
                            runKeyInvalid.Close();
                        }
                        RegistryKey runKeyInvalid1 = Registry.CurrentUser.OpenSubKey(runKeyList[1], true);
                        runKeyInvalid1?.DeleteValue(nameAutorun, false);
                        runKeyInvalid1.Close();
                        collDelete.Add(itemtoDelete);
                        continue;
                    }
                    else
                    {
                        continue;
                    }
                    // DELETING VALID ENTRY
                    startupKey?.DeleteValue(nameAutorun, false);
                    runKey?.DeleteValue(nameAutorun, false);
                    startupKey.Close();
                    runKey.Close();
                    collDelete.Add(itemtoDelete);
                }
                catch
                {
                    //ignored
                }
            registryViewHklm64.Close();
            foreach (RunItem item in collDelete)
            {
                AutorunsList.Remove(item);
            }
        }

        private static void GetEntries(RegistryKey entryKey, RegistryKey runKey, string groupName,
            ISet<string> brokenRun)
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
                        // IMPROVE IMPLEMENT...
                        if (cleanValue.LastIndexOf("%ProgramFiles%", StringComparison.Ordinal) != -1 &&
                            Environment.Is64BitOperatingSystem)
                        {
                            cleanValue =
                                cleanValue.Substring(cleanValue.LastIndexOf("%", StringComparison.Ordinal) + 1);
                            cleanValue =
                                Path.GetFullPath(Environment.GetEnvironmentVariable("ProgramW6432") + cleanValue);
                        }
                        AutorunsList.Add(new RunItem
                        {
                            Name = runName,
                            Path = Environment.ExpandEnvironmentVariables(cleanValue),
                            Enabled = runValueBytes[0] == 02 ? "Yes" : "No",
                            Group = groupName
                        });
                    }
                    else
                    {
                        if (!entryKey.GetValueNames().Contains(runName))
                            brokenRun.Add(runName);
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

        private void LvAutoruns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                BtnDisable.IsEnabled = BtnEnable.IsEnabled = BtnDelete.IsEnabled = true;
            else if (e.RemovedItems.Count > 0 && ((ListView) sender).SelectedItems.Count == 0)
                BtnDisable.IsEnabled = BtnEnable.IsEnabled = BtnDelete.IsEnabled = false;
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

        public class RunItem : INotifyPropertyChanged
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
            public string Name { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}