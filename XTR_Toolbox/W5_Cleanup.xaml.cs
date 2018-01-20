using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private ObservableCollection<CleanItem> _cleanList = new ObservableCollection<CleanItem>();
        private readonly List<CheckBoxDirs> _dirList = new List<CheckBoxDirs>();

        private static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        private readonly Dictionary<string, string> _dirPreset = new Dictionary<string, string>
        {
            {"Temporary Directory", Path.GetTempPath()},
            {"Win Temporary Directory", Path.Combine(WinDir, @"Temp")},
            {"Windows Installer Cache", Path.Combine(WinDir, @"Installer\$PatchCache$\Managed")},
            {"Windows Update Cache", Path.Combine(WinDir, @"SoftwareDistribution\Download")},
            {"Windows Logs Directory", Path.Combine(WinDir, @"Logs")},
            {"Prefetch Cache", Path.Combine(WinDir, @"Prefetch")},
            {
                "Crash Dump Directory",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"CrashDumps")
            },
            {"Steam Redist Packages", SteamLibraryDir()}
        };

        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private bool _tipShown;
        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window5()
        {
            InitializeComponent();
            InitDirBind();
            LbDir.ItemsSource = _dirList;
            LvCleaner.ItemsSource = _cleanList;
            _worker.DoWork += ScanWorker;
            _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        private void InitDirBind()
        {
            foreach (KeyValuePair<string, string> oneDir in _dirPreset)
            {
                _dirList.Add(new CheckBoxDirs {Text = oneDir.Key, Enabled = Directory.Exists(oneDir.Value)});
            }
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
                        foundFiles.AddRange(Directory.EnumerateFiles(root, ext).ToList());
                }
                catch (UnauthorizedAccessException)
                {
                }
            // DEPTH 0
            else if (depth == 0)
                try
                {
                    foundFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
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

        private static string SteamLibraryDir()
        {
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Steam\SteamApps\common");
            const string altPath = @"D:\SteamLibrary\SteamApps\common";
            return Directory.Exists(altPath) ? altPath : programFilesPath;
        }

        private void BtnAnalyze_Click(object sender, EventArgs e)
        {
            BtnAnalyze.IsEnabled = false;
            List<string> dirToScan = (from item in _dirList where item.Checked select _dirPreset[item.Text]).ToList();
            if (dirToScan.Count == 0)
            {
                MessageBox.Show(@"Select an option to start searching.");
                BtnAnalyze.IsEnabled = true;
                return;
            }

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
            else typeToScan.Add("*");

            _worker.RunWorkerAsync(new StartData(Convert.ToInt32(CbBoxLevel.SelectionBoxItem), typeToScan, dirToScan));
        }

        private struct StartData
        {
            public readonly int ScanDepth;
            public readonly List<string> TypesList;
            public readonly List<string> DirsLists;

            public StartData(int scanDepth, List<string> typesList, List<string> dirsLists)
            {
                ScanDepth = scanDepth;
                TypesList = typesList;
                DirsLists = dirsLists;
            }
        }

        private static void ScanWorker(object sender, DoWorkEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            StartData sD = (StartData) e.Argument;
            int scanDepth = sD.ScanDepth;
            int totalSize = 0;
            List<CleanItem> tempList = new List<CleanItem>();
            foreach (string baseDir in sD.DirsLists)
            {
                bool isSteam = false;
                if (baseDir.Equals(SteamLibraryDir()))
                {
                    scanDepth = 2; // WORKS FOR OTHERS BECAUSE LAST
                    isSteam = true;
                }

                foreach (string fileFull in GetDirectoryFilesLoop(baseDir, sD.TypesList, scanDepth, isSteam))
                {
                    string cleanedPath = fileFull.Replace(baseDir, "").TrimStart('\\');
                    float fileSize = new FileInfo(fileFull).Length / 1024 + 1;
                    tempList.Add(new CleanItem
                    {
                        Path = cleanedPath,
                        Size = fileSize + " KB",
                        Extension = Path.GetExtension(cleanedPath).ToLower(),
                        Group = baseDir
                    });
                    totalSize += Convert.ToInt32(fileSize) / 1000;
                }
            }

            sw.Stop();
            e.Result = new EndData(sw.ElapsedMilliseconds, totalSize, tempList);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null) return;
            EndData eD = (EndData) e.Result;
            Stopwatch sw = Stopwatch.StartNew();
            _cleanList = new ObservableCollection<CleanItem>(eD.TempList);
            LvCleaner.ItemsSource = _cleanList;
            if (BtnGroup.IsChecked != null && (bool) BtnGroup.IsChecked)
            {
                CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvCleaner.ItemsSource);
                PropertyGroupDescription groupBind = new PropertyGroupDescription("Group");
                view.GroupDescriptions.Add(groupBind);
            }

            sw.Stop();
            float mSec = eD.ScanTimeMsec + sw.ElapsedMilliseconds;
            LabelScanValue.Content = mSec < 1000
                ? mSec + " ms"
                : (mSec / 1000.00).ToString("0.00") + " sec";
            ((GridView) LvCleaner.View).Columns[0].Header =
                @"File Path (" + Convert.ToString(LvCleaner.Items.Count) + @" Files) - (" + eD.TotalSize + @" MB)";
            if (!_tipShown)
            {
                Shared.SnackBarTip(MainSnackbar);
                _tipShown = true;
            }

            BtnAnalyze.IsEnabled = true;
        }

        private struct EndData
        {
            public readonly List<CleanItem> TempList;
            public readonly float ScanTimeMsec;
            public readonly int TotalSize;

            public EndData(float scanTimeMsec, int totalSize, List<CleanItem> tempList)
            {
                TotalSize = totalSize;
                TempList = tempList;
                ScanTimeMsec = scanTimeMsec;
            }
        }

        private void BtnClean_Click(object sender, EventArgs e)
        {
            ((Button) sender).IsEnabled = false;
            for (int index = LvCleaner.SelectedItems.Count - 1; index >= 0; index--)
            {
                CleanItem cItem = (CleanItem) LvCleaner.SelectedItems[index];
                string combinedPath = Path.Combine(cItem.Group, cItem.Path);
                _cleanList.Remove(cItem);
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

        private void CheckBoxAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBoxDirs t in _dirList)
            {
                if (t.Enabled)
                    t.Checked = true;
            }
        }

        private void CheckBoxAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBoxDirs t in _dirList)
                t.Checked = false;
        }

        private void LbSidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count != 0 || e.AddedItems.Count == 0) return;
            CheckBoxDirs t = (CheckBoxDirs) e.AddedItems[0];
            if (t.Enabled)
                t.Checked = !t.Checked;
            LbDir.SelectedIndex = -1;
        }

        private void LvCleaner_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            BtnClean.IsEnabled = ((ListView) sender).SelectedItems.Count > 0;

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

        private class CheckBoxDirs : INotifyPropertyChanged
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

            public string Text { [UsedImplicitly] get; set; }
            public bool Enabled { [UsedImplicitly] get; set; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private class CleanItem
        {
            public string Extension { [UsedImplicitly] get; set; }
            public string Group { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }
            public string Size { [UsedImplicitly] get; set; }
        }
    }
}