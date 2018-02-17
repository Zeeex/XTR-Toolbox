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
        private const string InfoConst = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32Const = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string RunConst = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        private static readonly string[] GroupName =
            {"All Users (x64)", "Current User", "All Users", "Invalid (Broken)"};

        private readonly ObservableCollection<StartupModel> _autorunsList = new ObservableCollection<StartupModel>();
        private readonly Dictionary<string, string> _brokenRun = new Dictionary<string, string>();

        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window2()
        {
            InitializeComponent();
        }

        private void AddBroken()
        {
            foreach (KeyValuePair<string, string> kvp in _brokenRun)
                _autorunsList.Add(new StartupModel
                {
                    Name = kvp.Key,
                    Group = GroupName[GroupName.Length - 1],
                    RunReg = kvp.Value
                });
            _brokenRun.Clear();
        }

        private string AddHkcu()
        {
            try
            {
                RegistryKey info = Registry.CurrentUser.OpenSubKey(InfoConst);
                RegistryKey run = Registry.CurrentUser.OpenSubKey(RunConst);
                if (info != null && run != null)
                    GetEntries(info, run, GroupName[1]);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private string AddHklm()
        {
            try
            {
                RegistryKey runView = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                RegistryKey info = Registry.LocalMachine.OpenSubKey(InfoConst);
                RegistryKey run64 = runView.OpenSubKey(Run32Const);
                if (info != null && run64 != null)
                    GetEntries(info, run64, GroupName[2]);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private string AddHklm64()
        {
            try
            {
                RegistryKey runView = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                RegistryKey info64 = runView.OpenSubKey(InfoConst);
                RegistryKey run64 = runView.OpenSubKey(RunConst);
                if (info64 != null && run64 != null)
                    GetEntries(info64, run64, GroupName[0]);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private void AutorunsScan()
        {
            StringBuilder errorAll = new StringBuilder();
            List<string> regSector = new List<string>
            {
                AddHklm64(),
                AddHkcu(),
                AddHklm()
            };
            foreach (string error in regSector)
            {
                if (error != null)
                    errorAll.AppendLine(error);
            }

            if (errorAll.Length > 0)
                MessageBox.Show($"Errors found - {errorAll.Length} : \n{errorAll}");
            AddBroken();
            UpdateEnabledCount();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string tbPath = TbAutoPath.Text;
            string[] pathAndArgs = tbPath.TrimStart('\"').Split(new[] {'\"'}, 2);
            string path = pathAndArgs[0];
            if (!File.Exists(path)) return;
            string args = pathAndArgs.Length == 2 ? pathAndArgs[1] : string.Empty;
            string tbName = TbAutoName.Text;
            if (tbName.Trim().Length == 0) tbName = Path.GetFileNameWithoutExtension(path);
            int groupIndex = CBoxGroup.SelectedIndex == 0 ? 1 : 2;
            byte[] enableBytes = {0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            string infoKey = null, runKey = null;
            try
            {
                if (groupIndex == 1)
                {
                    using (RegistryKey info = Registry.CurrentUser.OpenSubKey(InfoConst, true))
                    {
                        if (info != null)
                        {
                            info.SetValue(tbName, tbPath, RegistryValueKind.String);
                            infoKey = info.ToString();
                        }
                    }

                    using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunConst, true))
                    {
                        if (run != null)
                        {
                            run.SetValue(tbName, enableBytes, RegistryValueKind.Binary);
                            runKey = run.ToString();
                        }
                    }
                }
                else
                {
                    RegistryKey runView = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    using (RegistryKey info = Registry.LocalMachine.OpenSubKey(InfoConst, true))
                    {
                        if (info != null)
                        {
                            info.SetValue(tbName, tbPath, RegistryValueKind.String);
                            infoKey = info.ToString();
                        }
                    }

                    using (RegistryKey run = runView.OpenSubKey(Run32Const, true))
                    {
                        if (run != null)
                        {
                            run.SetValue(tbName, enableBytes, RegistryValueKind.Binary);
                            runKey = run.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating entry! " + ex.Message);
            }

            if (infoKey == null || runKey == null) return;
            _autorunsList.Add(new StartupModel
            {
                Name = tbName,
                Icon = Shared.PathToIcon(path),
                Path = path,
                Args = args,
                Enabled = "Yes",
                Group = GroupName[groupIndex],
                RunReg = runKey
            });
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

        private void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBoxResult mB = MessageBox.Show("Are you sure you want to delete selected item(s)?",
                "Delete Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (mB == MessageBoxResult.No) return;
            for (int index = LvAutoruns.SelectedItems.Count - 1; index >= 0; index--)
                try
                {
                    StartupModel itemToDelete = (StartupModel) LvAutoruns.SelectedItems[index];
                    string deleteName = itemToDelete.Name;
                    if (string.IsNullOrEmpty(deleteName)) continue;
                    string itemGroup = itemToDelete.Group;
                    string runRegPath = itemToDelete.RunReg.Split(new[] {'\\'}, 2)[1];
                    RegistryKey infoKey, runKey;
                    using (RegistryKey reg64 =
                        RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    {
                        if (itemGroup == GroupName[0])
                        {
                            infoKey = runKey = reg64;
                        }
                        else if (itemGroup == GroupName[1])
                        {
                            infoKey = runKey = Registry.CurrentUser;
                        }
                        else if (itemGroup == GroupName[2])
                        {
                            infoKey = Registry.LocalMachine;
                            runKey = reg64;
                        }
                        else if (itemGroup == GroupName[GroupName.Length - 1])
                        {
                            reg64.OpenSubKey(runRegPath, true)?.DeleteValue(itemToDelete.Name, false);
                            Registry.CurrentUser.OpenSubKey(runRegPath, true)?.DeleteValue(itemToDelete.Name, false);
                            reg64.Close();
                            _autorunsList.Remove(itemToDelete);
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    infoKey?.OpenSubKey(InfoConst, true)
                        ?.DeleteValue(itemToDelete.Name, false);
                    runKey?.OpenSubKey(runRegPath, true)?.DeleteValue(itemToDelete.Name, false);
                    infoKey?.Close();
                    runKey?.Close();
                    _autorunsList.Remove(itemToDelete);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error deleting value: {((StartupModel) LvAutoruns.SelectedItems[index]).Name}\n{ex.Message}");
                }
        }

        private void GetEntries(RegistryKey infoKey, RegistryKey runKey, string groupName)
        {
            try
            {
                string[] infoValues = infoKey.GetValueNames();
                foreach (string infoValue in infoValues)
                foreach (string runValue in runKey.GetValueNames())
                {
                    if (runKey.GetValue(runValue).GetType().Name != "Byte[]") continue;
                    if (infoValue == runValue)
                    {
                        byte[] runValueBytes = (byte[]) runKey.GetValue(runValue);
                        string regPath = infoKey
                            .GetValue(infoValue, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)
                            .ToString();
                        string[] pathAndArgs = regPath.TrimStart('\"').Split(new[] {'\"'}, 2);
                        string path = pathAndArgs[0];
                        // IMPROVE...
                        if (path.LastIndexOf("%ProgramFiles%", StringComparison.Ordinal) != -1 &&
                            Environment.Is64BitOperatingSystem)
                        {
                            path = path.Substring(path.LastIndexOf("%", StringComparison.Ordinal) + 1);
                            path = Path.GetFullPath(Environment.GetEnvironmentVariable("ProgramW6432") + path);
                        }

                        string filePath = Environment.ExpandEnvironmentVariables(path);
                        _autorunsList.Add(new StartupModel
                        {
                            Name = runValue,
                            Icon = Shared.PathToIcon(filePath),
                            Path = filePath,
                            Args = pathAndArgs.Length == 2 ? pathAndArgs[1] : string.Empty,
                            Enabled = runValueBytes[0] == 02 ? "Yes" : "No",
                            Group = groupName,
                            RunReg = runKey.ToString()
                        });
                    }

                    else
                    {
                        if (!_brokenRun.ContainsKey(runValue) && !infoValues.Contains(runValue))
                            _brokenRun.Add(runValue, runKey.ToString());
                    }
                }

                infoKey.Close();
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

        private void RefreshCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _autorunsList.Clear();
            AutorunsScan();
        }

        private void StateChangeCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedUICommand eCommand)) return;
            bool isEnabled = eCommand == StartupCmd.Enable;
            byte[] itemState = isEnabled
                ? new byte[] {0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}
                : new byte[] {0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            string stringState = isEnabled ? "Yes" : "No";
            foreach (StartupModel itemToSet in LvAutoruns.SelectedItems)
                try
                {
                    RegistryKey runKey;
                    string itemGroup = itemToSet.Group;
                    if (itemGroup == GroupName[0] || itemGroup == GroupName[2])
                        runKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    else if (itemGroup == GroupName[1])
                        runKey = Registry.CurrentUser;
                    else
                        continue;
                    runKey.OpenSubKey(itemToSet.RunReg.Split(new[] {'\\'}, 2)[1], true)
                        ?.SetValue(itemToSet.Name, itemState, RegistryValueKind.Binary);
                    runKey?.Close();
                    itemToSet.Enabled = stringState;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error changing value: {itemToSet.Name}\n{ex.Message}");
                }

            UpdateEnabledCount();
        }

        private void TbAutoPath_TextChanged(object sender, TextChangedEventArgs e) => BtnCreate.IsEnabled =
            ((TextBox) sender).Text.Trim().Length != 0;

        private void UpdateEnabledCount() =>
            TbEnabledNum.Text = _autorunsList.Count(item => item.Enabled == "Yes").ToString();

        private void ValidItemCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LvAutoruns.SelectedItems.Cast<StartupModel>()
                .All(item => item.Group != GroupName[GroupName.Length - 1]);


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AutorunsScan();
            LvAutoruns.ItemsSource = _autorunsList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvAutoruns.ItemsSource);
            view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            Shared.SnackBarTip(MainSnackbar);
        }

        private class StartupModel : INotifyPropertyChanged
        {
            private string _enabled;

            public event PropertyChangedEventHandler PropertyChanged;
            public string Args { [UsedImplicitly] get; set; }

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
            public string RunReg { [UsedImplicitly] get; set; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public static class StartupCmd
    {
        public static readonly RoutedUICommand Delete = new RoutedUICommand("Delete", "Del",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.Delete)});

        public static readonly RoutedUICommand Dir = new RoutedUICommand("Open Directory", "Dir",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.O, ModifierKeys.Control)});

        public static readonly RoutedUICommand Enable = new RoutedUICommand("Enable", "En",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.E, ModifierKeys.Control)});

        public static readonly RoutedUICommand Disable = new RoutedUICommand("Disable", "Dis",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.D, ModifierKeys.Control)});

        public static readonly RoutedUICommand Clip = new RoutedUICommand("Copy to Clipboard", "Clip",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.C, ModifierKeys.Control)});

        public static readonly RoutedUICommand Refresh = new RoutedUICommand("Quick Refresh", "Refresh",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.F5)});
    }
}