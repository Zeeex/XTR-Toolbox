using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using JetBrains.Annotations;
using XTR_Toolbox.Classes;

namespace XTR_Toolbox
{
    public partial class Window5
    {
        private static readonly string SteamLibraryDir = GetSteamLibraryDir();

        private static bool _befDate;
        private static string _date;

        private readonly ICollection<CheckBoxModel> _dirList = new Collection<CheckBoxModel>();
        private readonly IndicatorModel _indicatorBind = new IndicatorModel();
        private ObservableCollection<CleanModel> _cleanList;
        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;
        private int _scanElapsed;
        private readonly DispatcherTimer _dispatcherTimer = new DispatcherTimer {Interval = new TimeSpan(0, 0, 1)};

        public Window5()
        {
            InitializeComponent();
        }

        private static float AddModelToTemp(string baseDir, ICollection<CleanModel> tempList, string filePath)
        {
            float fileSize;
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                fileSize = fileInfo.Length / 1024 + 1;
                string cleanedPath = filePath.Replace(baseDir, "").TrimStart('\\');
                tempList.Add(new CleanModel
                {
                    Path = cleanedPath,
                    Size = fileSize + " KB",
                    Date = fileInfo.LastAccessTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Extension = Path.GetExtension(cleanedPath).ToLower(),
                    Group = baseDir
                });
            }
            catch (FileNotFoundException)
            {
                return 0;
            }

            return fileSize;
        }

        private static void DeleteOp(IEnumerable input, ICollection<CleanModel> output, IProgress<int> progress)
        {
            int co = 0;
            foreach (CleanModel item in input)
            {
                string combinedPath = Path.Combine(item.Group, item.Path);
                progress.Report(++co);
                try
                {
                    File.Delete(combinedPath);
                    output.Add(item);
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

        private static void DirectorySearch(IReadOnlyCollection<string> dirs, string multiExt,
            ICollection<CleanModel> tempList, int depth, bool isSteam, ref float maxSize)
        {
            if (dirs.Any())
            {
                ICollection<string> typeToScan = new Collection<string>();
                if (multiExt != "")
                {
                    string[] split = multiExt.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
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

                maxSize += dirs.Sum(baseDir =>
                    GetDirectoryFilesLoop(baseDir, typeToScan, depth)
                        .Sum(fileFull => AddModelToTemp(baseDir, tempList, fileFull) / 1024));
            }

            if (isSteam)
                SteamLibrarySearch(SteamLibraryDir, tempList, ref maxSize);
        }

        private static IEnumerable<string> GetDirectoryFilesLoop(string root, IEnumerable<string> types, int depth)
        {
            List<string> foundFiles = new List<string>();
            IEnumerable<string> typeEnum = types as string[] ?? types.ToArray();

            foreach (string ext in typeEnum)
                try
                {
                    if (_date.Length == 0)
                    {
                        foundFiles.AddRange(Directory.EnumerateFiles(root, ext));
                    }
                    else
                    {
                        ICollection<string> dateList = new Collection<string>();
                        foreach (string item in Directory.EnumerateFiles(root, ext))
                        {
                            FileInfo ii = new FileInfo(item);
                            if (_befDate)
                            {
                                if (ii.LastAccessTime < DateTime.Parse(_date))
                                    dateList.Add(item);
                            }
                            else if (ii.LastAccessTime >= DateTime.Parse(_date))
                            {
                                dateList.Add(item);
                            }
                        }

                        foundFiles.AddRange(dateList);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }

            if (depth <= 0) return foundFiles;
            try
            {
                foreach (string subDir in Directory.EnumerateDirectories(root))
                    foundFiles.AddRange(GetDirectoryFilesLoop(subDir, typeEnum, depth - 1));
            }
            catch (UnauthorizedAccessException)
            {
            }

            return foundFiles;
        }

        private static string GetSteamLibraryDir()
        {
            string[] drives = DriveInfo.GetDrives()
                .Where(info => info.DriveType == DriveType.Fixed).Select(info => info.Name).ToArray();
            string[] dirs =
            {
                drives[1] + @"SteamLibrary\SteamApps\common",
                drives[0] + @"SteamLibrary\SteamApps\common",
                Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), @"Steam\SteamApps\common"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Steam\SteamApps\common")
            };
            foreach (string one in dirs)
            {
                if (!Directory.Exists(one)) continue;
                return one;
            }

            return dirs[0];
        }

        private static void SteamLibrarySearch(string root, ICollection<CleanModel> tempList, ref float totalSize)
        {
            string[] patternName = {"Redist", "directx", "setup", "depends"};
            try
            {
                foreach (string gameDir in Directory.EnumerateDirectories(root))
                foreach (string pattDir in Directory.EnumerateDirectories(gameDir))
                {
                    if (!patternName.Any(pattDir.Contains)) continue;
                    try
                    {
                        totalSize += Directory.EnumerateFiles(pattDir, "*", SearchOption.AllDirectories)
                            .Sum(filePath => AddModelToTemp(root, tempList, filePath) / 1024);
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
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

        private void DeleteCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LvCleaner.SelectedItems.Count > 0;

        private async void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_indicatorBind.DeleteIndicator) return;
            _indicatorBind.DeleteIndicator = true;
            IList input = LvCleaner.SelectedItems;
            List<CleanModel> output = new List<CleanModel>();
            Progress<int> progress = new Progress<int>(s => _indicatorBind.DeleteProgress = s);
            await Task.Run(() => DeleteOp(input, output, progress));
            output.ForEach(cm => _cleanList.Remove(cm));
            _indicatorBind.DeleteIndicator = false;
        }

        private void DirSetup()
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            Dictionary<string, string> dirPreset = new Dictionary<string, string>
            {
                {"Temporary Directory", Path.GetTempPath()},
                {"Win Temporary Directory", Path.Combine(winDir, @"Temp")},
                {"Windows Installer Cache", Path.Combine(winDir, @"Installer\$PatchCache$\Managed")},
                {"Windows Update Cache", Path.Combine(winDir, @"SoftwareDistribution\Download")},
                {"Windows Logs Directory", Path.Combine(winDir, @"Logs")},
                {"Prefetch Cache", Path.Combine(winDir, @"Prefetch")},
                {
                    "Crash Dump Directory",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"CrashDumps")
                },
                {"Google Chrome Cache", Path.Combine(Window7.ChromeDataPath, @"Default\Cache")},
                {"Steam Redist Packages", SteamLibraryDir}
            };
            foreach (KeyValuePair<string, string> oneDir in dirPreset)
                _dirList.Add(new CheckBoxModel(oneDir.Key, oneDir.Value, Directory.Exists(oneDir.Value)));
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

        private void ScanCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LbDir.Items.Cast<CheckBoxModel>().Any(item => item.Checked);

        private async void ScanCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnAnalyze.Focus(); // NEED TO FOCUS BECAUSE OF DATE PICKER
            if (_indicatorBind.ScanIndicator) return;
            MainSnackbar.IsActive = false;
            _indicatorBind.ScanIndicator = true;
            _scanElapsed = 1;
            Stopwatch stopwatchScan = Stopwatch.StartNew();
            _dispatcherTimer.Start();
            bool isSteam = false;
            List<string> selectedDirs = new List<string>();
            foreach (CheckBoxModel item in _dirList)
            {
                if (!item.Checked) continue;
                if (item.Path.Equals(SteamLibraryDir))
                {
                    isSteam = true;
                    continue;
                }

                if (Directory.Exists(item.Path))
                    selectedDirs.Add(item.Path);
            }

            float maxSize = 0;
            string multiExt = TBoxTypes.Text.Trim();
            int depth = Convert.ToInt32(CbBoxLevel.SelectionBoxItem);
            _date = DateFilter.Text;
            _befDate = TglDate.IsChecked != null && (bool) !TglDate.IsChecked;
            ICollection<CleanModel> tList = new List<CleanModel>();
            await Task.Run(() =>
                DirectorySearch(selectedDirs, multiExt, tList, depth, isSteam, ref maxSize));

            _dispatcherTimer.Stop();
            stopwatchScan.Stop();
            _cleanList = new ObservableCollection<CleanModel>(tList);
            LvCleaner.ItemsSource = _cleanList;

            if (CheckBoxGroup.IsChecked != null && (bool) CheckBoxGroup.IsChecked)
            {
                CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvCleaner.ItemsSource);
                view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            }

            _indicatorBind.ScanTime = FormatScanTime(stopwatchScan.ElapsedMilliseconds);
            _indicatorBind.MaxSize = Convert.ToInt32(maxSize);
            _indicatorBind.ScanIndicator = false;
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e) =>
            _indicatorBind.ScanTime = $"{_scanElapsed++} sec";

        private static string FormatScanTime(double mSec) => mSec < 1000
            ? $"{mSec} ms"
            : $"{mSec / 1000:0.00} sec";

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            Shared.FitWindow.Init(Width, Height);
            DirSetup();
            LbDir.ItemsSource = _dirList;
            CbBoxLevel.ItemsSource = new[] {0, 1, 2, 3, 4, 5, 20};
            CbBoxLevel.SelectedIndex = 5;
            StackPanelBtns.DataContext = TbHeader.DataContext = _indicatorBind;
            MainSnackbarMessage.Content = "Selecting many items at once can slow scan operation significantly";
            MainSnackbar.IsActive = true;
            _dispatcherTimer.Tick += DispatcherTimer_Tick;
        }

        private class CheckBoxModel : INotifyPropertyChanged
        {
            private bool _checked;

            public CheckBoxModel(string text, string path, bool enabled)
            {
                Text = text;
                Path = path;
                Enabled = enabled;
            }

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

            public bool Enabled { [UsedImplicitly] get; }
            public string Path { [UsedImplicitly] get; }
            public string Text { [UsedImplicitly] get; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private class CleanModel
        {
            public string Date { [UsedImplicitly] get; set; }
            public string Extension { [UsedImplicitly] get; set; }
            public string Group { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }
            public string Size { [UsedImplicitly] get; set; }
        }

        private class IndicatorModel : INotifyPropertyChanged
        {
            private bool _deleteIndicator;
            private int _deleteProgress;
            private int _maxSize;
            private bool _scanIndicator;
            private string _scanTime;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool DeleteIndicator
            {
                [UsedImplicitly] get => _deleteIndicator;
                set => SetField(ref _deleteIndicator, value);
            }

            public int DeleteProgress
            {
                [UsedImplicitly] get => _deleteProgress;
                set => SetField(ref _deleteProgress, value);
            }

            public int MaxSize
            {
                [UsedImplicitly] get => _maxSize;
                set => SetField(ref _maxSize, value);
            }

            public bool ScanIndicator
            {
                [UsedImplicitly] get => _scanIndicator;
                set => SetField(ref _scanIndicator, value);
            }

            public string ScanTime
            {
                [UsedImplicitly] get => _scanTime;
                set => SetField(ref _scanTime, value);
            }

            private void SetField<T>(ref T field, T value, [CallerMemberName] string propName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
    }

    public static class CleanCmd
    {
        public static readonly RoutedCommand Delete = new RoutedCommand("Delete", typeof(CleanCmd),
            new InputGestureCollection {new KeyGesture(Key.Delete, ModifierKeys.Control)});

        public static readonly RoutedCommand Scan = new RoutedCommand("Scan", typeof(CleanCmd),
            new InputGestureCollection {new KeyGesture(Key.S, ModifierKeys.Control)});
    }
}