using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public partial class Window2
    {
        private const string InfoConst = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32Const = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string RunConst = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        private static readonly string[] GroupName =
            {"All Users (x64)", "Current User", "All Users", "Invalid (Broken)"};

        private static readonly ObservableCollection<StartupLogModel> HistoryList =
            new ObservableCollection<StartupLogModel>();

        private static bool _enableLog;

        private ObservableCollection<StartupModel> _autorunsList;

        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window2()
        {
            InitializeComponent();
        }

        private static string AddHkcu(ICollection<StartupModel> mainList)
        {
            try
            {
                RegistryKey info = Registry.CurrentUser.OpenSubKey(InfoConst);
                RegistryKey run = Registry.CurrentUser.OpenSubKey(RunConst);
                if (info != null && run != null)
                    GetEntries(info, run, GroupName[1], mainList);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string AddHklm(ICollection<StartupModel> mainList)
        {
            try
            {
                RegistryKey runView = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                RegistryKey info = Registry.LocalMachine.OpenSubKey(InfoConst);
                RegistryKey run64 = runView.OpenSubKey(Run32Const);
                if (info != null && run64 != null)
                    GetEntries(info, run64, GroupName[2], mainList);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string AddHklm64(ICollection<StartupModel> mainList)
        {
            try
            {
                RegistryKey runView = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                RegistryKey info64 = runView.OpenSubKey(InfoConst);
                RegistryKey run64 = runView.OpenSubKey(RunConst);
                if (info64 != null && run64 != null)
                    GetEntries(info64, run64, GroupName[0], mainList);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void GetEntries(RegistryKey infoKey, RegistryKey runKey, string groupName,
            ICollection<StartupModel> tList)
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
                        tList.Add(new StartupModel
                        {
                            Name = runValue,
                            Icon = Shared.PathToIcon(filePath),
                            Path = filePath,
                            Args = pathAndArgs.Length == 2 ? pathAndArgs[1] : string.Empty,
                            Enabled = runValueBytes[0] == 02,
                            Group = groupName,
                            RunReg = runKey.ToString()
                        });
                    }

                    else if (!infoValues.Contains(runValue))
                    {
                        tList.Add(new StartupModel
                        {
                            Name = runValue,
                            Group = GroupName[GroupName.Length - 1],
                            RunReg = runKey.ToString()
                        });
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

        private async void CreateCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            StartupCreateUc startupCreate = new StartupCreateUc();
            object result = await DialogHost.Show(startupCreate, "RootDialog");
            if (result == null || result.ToString() != "Y") return;

            string tbPath = startupCreate.TbAutoPath.Text;
            string[] pathAndArgs = tbPath.TrimStart('\"').Split(new[] {'\"'}, 2);
            string path = pathAndArgs[0];
            if (!File.Exists(path)) return;
            string args = pathAndArgs.Length == 2 ? pathAndArgs[1] : string.Empty;
            string tbName = startupCreate.TbAutoName.Text;
            if (tbName.Trim().Length == 0) tbName = Path.GetFileNameWithoutExtension(path);
            int groupIndex = startupCreate.CBoxGroup.SelectedIndex == 0 ? 1 : 2;
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
                Enabled = true,
                Group = GroupName[groupIndex],
                RunReg = runKey
            });
            HistoryList.Add(
                new StartupLogModel {History = $"{DateTime.Now.ToShortTimeString()} - {tbName} : Created"});
        }

        private async void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int selCount = LvAutoruns.SelectedItems.Count;
            object result = await DialogHost.Show(
                new GenYesNoUc($"Are you sure you want to delete {selCount} items?"), "RootDialog");
            if (result == null || result.ToString() != "Y") return;
            for (int index = selCount - 1; index >= 0; index--)
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
                            HistoryList.Add(
                                new StartupLogModel
                                {
                                    History = $"{DateTime.Now.ToShortTimeString()} - {itemToDelete.Name} : Deleted"
                                });
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
                    HistoryList.Add(
                        new StartupLogModel
                        {
                            History = $"{DateTime.Now.ToShortTimeString()} - {itemToDelete.Name} : Deleted"
                        });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error deleting value: {((StartupModel) LvAutoruns.SelectedItems[index]).Name}\n{ex.Message}");
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
                        CustomProc.StartProc(dirFull, wait: false);
                }
            }
            catch
            {
                //ignored
            }
        }

        private void RefreshCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            StartupScan();
        }

        private void StartupScan()
        {
            StringBuilder errorAll = new StringBuilder();
            List<StartupModel> mainList = new List<StartupModel>();
            List<string> regSector = new List<string>
            {
                AddHklm64(mainList),
                AddHkcu(mainList),
                AddHklm(mainList)
            };
            foreach (string error in regSector)
                if (error != null)
                    errorAll.AppendLine(error);

            if (errorAll.Length > 0)
                MessageBox.Show($"Errors found - {errorAll.Length} : \n{errorAll}");

            _autorunsList = new ObservableCollection<StartupModel>(mainList);
            LvAutoruns.ItemsSource = _autorunsList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvAutoruns.ItemsSource);
            view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            view.SortDescriptions.Add(new SortDescription("Path", ListSortDirection.Descending));
            UpdateEnabledCount();
        }

        private void StateChangeCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedUICommand eCommand)) return;
            bool isEnabled = eCommand == StartupCmd.Enable;
            byte[] itemState = isEnabled
                ? new byte[] {0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}
                : new byte[] {0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
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
                    itemToSet.Enabled = isEnabled;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error changing value: {itemToSet.Name}\n{ex.Message}");
                }

            UpdateEnabledCount();
        }

        private void UpdateEnabledCount() =>
            TbEnabledNum.Text = _autorunsList.Count(item => item.Enabled).ToString();

        private void ValidItemCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LvAutoruns.SelectedItems.Cast<StartupModel>()
                .All(item => item.Group != GroupName[GroupName.Length - 1]);


        private void Window_ContentRendered(object sender, EventArgs e)
        {
            Shared.FitWindow.Init(Width, Height);
            _enableLog = false;
            StartupScan();
            LbHistory.ItemsSource = HistoryList;
            _enableLog = true;
            Shared.ShowSnackBar(MainSnackbar);
        }

        private class StartupLogModel
        {
            public string History { [UsedImplicitly] get; set; }
        }

        private class StartupModel : INotifyPropertyChanged
        {
            private bool _enabled;

            public event PropertyChangedEventHandler PropertyChanged;
            public string Args { [UsedImplicitly] get; set; }

            public bool Enabled
            {
                [UsedImplicitly] get => _enabled;
                set
                {
                    if (!SetField(ref _enabled, value) || !_enableLog) return;
                    string stat = value ? "Enabled" : "Disabled";
                    HistoryList.Add(
                        new StartupLogModel {History = $"{DateTime.Now.ToShortTimeString()} - {Name} : {stat}"});
                }
            }

            public string Group { [UsedImplicitly] get; set; }
            public BitmapSource Icon { [UsedImplicitly] get; set; }
            public string Name { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }
            public string RunReg { [UsedImplicitly] get; set; }

            private bool SetField(ref bool field, bool value, [CallerMemberName] string propName = null)
            {
                if (field == value) return false;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
                return true;
            }
        }
    }

    public static class StartupCmd
    {
        public static readonly RoutedUICommand Clip = new RoutedUICommand("Copy to Clipboard", "Clip",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.C, ModifierKeys.Control)});

        public static readonly RoutedCommand Create = new RoutedCommand("Create",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.N, ModifierKeys.Control)});

        public static readonly RoutedUICommand Delete = new RoutedUICommand("Delete", "Del",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.Delete)});

        public static readonly RoutedUICommand Dir = new RoutedUICommand("Open Directory", "Dir",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.O, ModifierKeys.Control)});

        public static readonly RoutedUICommand Disable = new RoutedUICommand("Disable", "Dis",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.D, ModifierKeys.Control)});

        public static readonly RoutedUICommand Enable = new RoutedUICommand("Enable", "En",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.E, ModifierKeys.Control)});

        public static readonly RoutedUICommand Refresh = new RoutedUICommand("Quick Refresh", "Refresh",
            typeof(StartupCmd), new InputGestureCollection {new KeyGesture(Key.F5)});
    }
}