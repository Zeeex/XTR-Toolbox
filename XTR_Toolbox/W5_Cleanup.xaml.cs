using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using JetBrains.Annotations;

namespace XTR_Toolbox
{
    public partial class Window5
    {
        private static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        private readonly List<CheckBoxModel> _dirList = new List<CheckBoxModel>();
        private readonly ScanModel _scanBind = new ScanModel();
        private ObservableCollection<CleanModel> _cleanList;
        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        private bool _tipShown;

        public Window5()
        {
            InitializeComponent();
            DirSetup();
        }

        private static IEnumerable<string> GetDirectoryFilesLoop(string root, IEnumerable<string> types, int depth,
            bool isSteam)
        {
            IEnumerable<string> typeListArray = types as string[] ?? types.ToArray();
            List<string> foundFiles = new List<string>();

            // FILES NOT STEAM
            if (!isSteam)
                try
                {
                    foreach (string ext in typeListArray)
                        foundFiles.AddRange(Directory.EnumerateFiles(root, ext));
                }
                catch (UnauthorizedAccessException)
                {
                }
            // DEPTH 0
            else if (depth == 0)
                try
                {
                    foundFiles.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
                }
                catch (UnauthorizedAccessException)
                {
                }

            if (depth <= 0) return foundFiles;
            try
            {
                IEnumerable<string> folders = Directory.EnumerateDirectories(root);
                foreach (string folder in folders)
                {
                    if (isSteam && depth == 1)
                    {
                        string[] dirPattern = {"Redist", "directx", "setup", "depends"};
                        if (!dirPattern.Any(folder.Contains))
                            continue;
                    }

                    foundFiles.AddRange(GetDirectoryFilesLoop(folder, typeListArray, depth - 1, isSteam));
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (PathTooLongException)
            {
            }

            return foundFiles;
        }

        private static void ScanOneDir(IEnumerable<string> typeToScan, ref int scanDepth, ref float totalSize,
            ICollection<CleanModel> tempList, string baseDir)
        {
            bool isSteam = baseDir.Equals(SteamLibraryDir());
            if (isSteam)
                scanDepth = 2; // WORKS FOR OTHERS BECAUSE LAST

            float ts = 0;
            Parallel.ForEach(GetDirectoryFilesLoop(baseDir, typeToScan, scanDepth, isSteam), fileFull =>
            {
                string cleanedPath = fileFull.Replace(baseDir, "").TrimStart('\\');
                float fileSize = new FileInfo(fileFull).Length / 1024 + 1;
                tempList.Add(new CleanModel
                {
                    Path = cleanedPath,
                    Size = fileSize + " KB",
                    Extension = Path.GetExtension(cleanedPath).ToLower(),
                    Group = baseDir
                });
                ts += fileSize / 1024;
            });
            totalSize += ts;
        }

        private static string SteamLibraryDir()
        {
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Steam\SteamApps\common");
            const string altPath = @"D:\SteamLibrary\SteamApps\common";
            return Directory.Exists(altPath) ? altPath : programFilesPath;
        }

        private void CheckBoxAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBoxModel t in _dirList)
                if (t.Enabled)
                    t.Checked = true;
        }

        private void CheckBoxAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBoxModel t in _dirList)
                t.Checked = false;
        }

        private void DeleteCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LvCleaner.SelectedItems.Count > 0;
        }

        private void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            for (int index = LvCleaner.SelectedItems.Count - 1; index >= 0; index--)
            {
                CleanModel item = (CleanModel) LvCleaner.SelectedItems[index];
                string combinedPath = Path.Combine(item.Group, item.Path);
                _cleanList.Remove(item);
                try
                {
                    File.Delete(combinedPath);
                    string directoryName = Path.GetDirectoryName(combinedPath);
                    if (directoryName != null)
                        Directory.Delete(directoryName);
                }
                catch
                {
                    //ignored
                }
            }
        }

        private void DirSetup()
        {
            Dictionary<string, string> dirPreset = new Dictionary<string, string>
            {
                {"Temporary Directory", Path.GetTempPath()},
                {"Win Temporary Directory", Path.Combine(WinDir, @"Temp")},
                {"Windows Installer Cache", Path.Combine(WinDir, @"Installer\$PatchCache$\Managed")},
                {"Windows Update Cache", Path.Combine(WinDir, @"SoftwareDistribution\Download")},
                {"Windows Logs Directory", Path.Combine(WinDir, @"Logs")},
                {"Prefetch Cache", Path.Combine(WinDir, @"Prefetch")},
                {
                    "Crash Dump Directory",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"CrashDumps")
                },
                {"Steam Redist Packages", SteamLibraryDir()}
            };
            foreach (KeyValuePair<string, string> oneDir in dirPreset)
                _dirList.Add(new CheckBoxModel
                {
                    Text = oneDir.Key,
                    Path = oneDir.Value,
                    Enabled = Directory.Exists(oneDir.Value)
                });
        }

        private void LbSidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count != 0 || e.AddedItems.Count == 0) return;
            CheckBoxModel t = (CheckBoxModel) e.AddedItems[0];
            if (t.Enabled)
                t.Checked = !t.Checked;
            LbDir.SelectedIndex = -1;
        }

        private void LvUsersColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = sender as GridViewColumnHeader;
            string sortBy = column?.Tag.ToString();
            if (_listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Remove(_listViewSortAdorner);
                LvCleaner.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (Equals(_listViewSortCol, column) && Equals(_listViewSortAdorner.Direction, newDir))
                newDir = ListSortDirection.Descending;

            _listViewSortCol = column;
            _listViewSortAdorner = new SortAdorner(_listViewSortCol, newDir);
            if (_listViewSortCol != null)
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Add(_listViewSortAdorner);
            if (sortBy != null)
                LvCleaner.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void ScanCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LbDir.Items.Cast<CheckBoxModel>().Any(item => item.Checked);
        }

        private async void ScanCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnAnalyze.IsEnabled = false;
            IEnumerable<string> dirsEnum = from item in _dirList where item.Checked where Directory.Exists(item.Path) select item.Path;
            List<string> typeToScan = new List<string>();
            string ext = TBoxTypes.Text.Trim();
            if (ext != "")
            {
                string[] split = ext.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                foreach (string type in split)
                {
                    string trimmed = type.Trim();
                    if (trimmed.StartsWith("*"))
                        typeToScan.Add(trimmed);
                    if (trimmed.StartsWith("."))
                        typeToScan.Add("*" + trimmed);
                }
            }
            else
            {
                typeToScan.Add("*");
            }

            Stopwatch sw = Stopwatch.StartNew();
            float totalSize = 0;
            int scanDepth = Convert.ToInt32(CbBoxLevel.SelectionBoxItem);
            List<CleanModel> tempList = new List<CleanModel>();

            IEnumerable<Task> tasks = dirsEnum.Select(baseDir =>
                Task.Run(() => ScanOneDir(typeToScan, ref scanDepth, ref totalSize, tempList, baseDir)));

            await Task.WhenAll(tasks);

            _cleanList = new ObservableCollection<CleanModel>(tempList);
            LvCleaner.ItemsSource = _cleanList;

            if (BtnGroup.IsChecked != null && (bool) BtnGroup.IsChecked)
            {
                CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvCleaner.ItemsSource);
                PropertyGroupDescription groupBind = new PropertyGroupDescription("Group");
                view.GroupDescriptions.Add(groupBind);
            }

            sw.Stop();

            float mSec = sw.ElapsedMilliseconds;
            _scanBind.ScanTime = mSec < 1000
                ? mSec + " ms"
                : (mSec / 1000).ToString("0.00") + " sec";
            _scanBind.TotalSize = Convert.ToInt32(totalSize).ToString();

            if (!_tipShown)
            {
                Shared.SnackBarTip(MainSnackbar);
                _tipShown = true;
            }

            BtnAnalyze.IsEnabled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LbDir.ItemsSource = _dirList;
            LabelScanValue.DataContext = TbHeader.DataContext = _scanBind;
        }

        private class CheckBoxModel : INotifyPropertyChanged
        {
            private bool _checked;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool Checked
            {
                get => _checked;
                set
                {
                    if (_checked == value) return;
                    _checked = value;
                    NotifyPropertyChanged(nameof(Checked));
                }
            }

            public bool Enabled { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }

            public string Text { [UsedImplicitly] get; set; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private class CleanModel
        {
            public string Extension { [UsedImplicitly] get; set; }
            public string Group { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }
            public string Size { [UsedImplicitly] get; set; }
        }

        private class ScanModel : INotifyPropertyChanged
        {
            private string _scanTime;
            private string _totalSize = "0";

            public event PropertyChangedEventHandler PropertyChanged;

            public string ScanTime
            {
                [UsedImplicitly] get => _scanTime;

                set
                {
                    if (_scanTime == value) return;
                    _scanTime = value;
                    NotifyPropertyChanged(nameof(ScanTime));
                }
            }

            public string TotalSize
            {
                [UsedImplicitly] get => _totalSize;

                set
                {
                    if (_totalSize == value) return;
                    _totalSize = value;
                    NotifyPropertyChanged(nameof(TotalSize));
                }
            }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public static class CleanCmd
    {
        public static readonly RoutedCommand Delete = new RoutedCommand("Delete", typeof(CleanCmd),
            new InputGestureCollection {new KeyGesture(Key.Delete, ModifierKeys.Control)});

        public static readonly RoutedCommand Scan = new RoutedCommand("Dir", typeof(CleanCmd),
            new InputGestureCollection {new KeyGesture(Key.S, ModifierKeys.Control)});
    }
}