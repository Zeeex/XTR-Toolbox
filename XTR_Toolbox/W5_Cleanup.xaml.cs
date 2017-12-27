using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using JetBrains.Annotations;

namespace XTR_Toolbox
{
    public partial class Window5
    {
        private readonly ObservableCollection<CleanItem> _cleanList = new ObservableCollection<CleanItem>();
        private readonly List<CheckBoxSidebar> _dirList = new List<CheckBoxSidebar>();
        private readonly List<CheckBoxSidebar> _typeList = new List<CheckBoxSidebar>();
        private readonly string _extDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        private readonly string[] _fixedDirArray =
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Installer\$PatchCache$\Managed"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"SoftwareDistribution\Download"),
            SteamLibraryDir() // ALWAYS LAST
        };

        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window5()
        {
            InitializeComponent();
            FillSidebar();
            LbDir.ItemsSource = _dirList;
            LbType.ItemsSource = _typeList;
            LvCleaner.ItemsSource = _cleanList;
        }

        private void FillSidebar()
        {
            string[] dirsList =
            {
                "Temporary Directory",
                "Win Temporary Directory",
                "Windows Installer Cache",
                "Windows Update Cache",
                "Steam Redist Pacakges"
            };
            string[] typeList =
            {
                "Temp Files (.tmp)",
                "Cache Files (.cache)",
                "Crash Dump Files (.dmp)",
                "System Logs (.log)",
                "Backup Files (.bak)",
                "Shortcut Files (.lnk)",
                "Executable Files (.exe)",
                "Win Installer Files (.msi)"
            };
            foreach (string adder in dirsList)
                _dirList.Add(new CheckBoxSidebar { Text = adder });
            foreach (string adder in typeList)
                _typeList.Add(new CheckBoxSidebar { Text = adder });
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
                        foundFiles = Directory.EnumerateFiles(root, ext).ToList();
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
            List<string> dirList = new List<string>();
            // DIR ENUM ====
            for (int index = 0; index < _dirList.Count; index++)
            {
                CheckBoxSidebar item = _dirList[index];
                if (!item.Checked) continue;
                dirList.Add(_fixedDirArray[index]);
            }
            int dirCount = dirList.Count;
            if (dirCount == 0)
                dirList.Add(_extDir);
            // TYPE ENUM ====
            List<string> typeList = new List<string> {"*"};
            if (_typeList.Any(item => item.Checked))
            {
                typeList.Clear();
                typeList.AddRange(_typeList.Where(item => item.Checked)
                    .Select(item => item.Text)
                    .Select(tempClean =>
                        "*" + tempClean.Substring(tempClean.IndexOf("(.", StringComparison.Ordinal) + 1).TrimEnd(')')));
            }
            else if (dirCount == 0)
            {
                MessageBox.Show(@"Select an option for searching.");
                return;
            }
            // DEFAULT VALUES =====
            ((Button) sender).IsEnabled = false;
            _cleanList.Clear();
            int totalSize = 0;
            int scanDepth = CbBoxLevel.SelectedIndex == CbBoxLevel.Items.Count - 1
                ? 20
                : Convert.ToInt32(CbBoxLevel.SelectionBoxItem);
            // DEFAULT VALUES END =====
            Stopwatch sw = Stopwatch.StartNew();
            foreach (string oneDir in dirList)
            {
                bool isSteam = false;
                if (oneDir.Equals(SteamLibraryDir()))
                {
                    scanDepth = 2; // WORKS FOR OTHERS BECAUSE LAST
                    isSteam = true;
                    // BENCHMARK - OLD STEAM SCAN .11-.13 , NEW .16-.18
                }
                if (!Directory.Exists(oneDir))
                    continue;
                try
                {
                    foreach (string fileFull in GetDirectoryFilesLoop(oneDir, typeList, scanDepth, isSteam))
                    {
                        string cleanedPath = fileFull.Replace(oneDir, "").TrimStart('\\');
                        float fileSize = new FileInfo(fileFull).Length / 1024 + 1;
                        _cleanList.Add(new CleanItem
                        {
                            Path = cleanedPath,
                            Size = fileSize + " KB",
                            Extension = Path.GetExtension(cleanedPath).ToLower(),
                            Group = oneDir
                        });
                        totalSize += Convert.ToInt32(fileSize) / 1000;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    //ignored
                }
                catch (Exception ex)
                {
                    MessageBox.Show(@"Scan Error: " + ex.Message);
                }
            }
            sw.Stop();
            ((Button) sender).IsEnabled = true;
            float mSec = sw.ElapsedMilliseconds;
            LabelScanValue.Content = mSec < 1000
                ? mSec + " ms"
                : (mSec / 1000.00).ToString("0.00") + " sec";
            ((GridView) LvCleaner.View).Columns[0].Header =
                @"File Path (" + Convert.ToString(LvCleaner.Items.Count) + @" Files) - (" +
                totalSize + @" MB)";
        }

        private void BtnClean_Click(object sender, EventArgs e)
        {
            ((Button) sender).IsEnabled = false;
            for (int index = LvCleaner.SelectedItems.Count - 1; index >= 0; index--)
            {
                CleanItem cItem = (CleanItem)LvCleaner.SelectedItems[index];
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
            switch (((CheckBox) sender).Name)
            {
                case nameof(CbDir):
                    foreach (CheckBoxSidebar t in _dirList)
                        t.Checked = true;
                    break;
                case nameof(CbType):
                    foreach (CheckBoxSidebar t in _typeList)
                        t.Checked = true;
                    break;
            }
        }

        private void CheckBoxAll_Unchecked(object sender, RoutedEventArgs e)
        {
            switch (((CheckBox) sender).Name)
            {
                case nameof(CbDir):
                    foreach (CheckBoxSidebar t in _dirList)
                        t.Checked = false;
                    break;
                case nameof(CbType):
                    foreach (CheckBoxSidebar t in _typeList)
                        t.Checked = false;
                    break;
            }
        }

        private void LbSidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count != 0 || e.AddedItems.Count == 0) return;
            CheckBoxSidebar t = (CheckBoxSidebar) e.AddedItems[0];
            t.Checked = !t.Checked;
            LbDir.SelectedIndex = LbType.SelectedIndex = -1;
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

        private class CheckBoxSidebar : INotifyPropertyChanged
        {
            private bool _checked;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool Checked
            {
                [UsedImplicitly] get => _checked;
                set
                {
                    if (_checked == value) return;
                    _checked = value;
                    NotifyPropertyChanged(nameof(Checked));
                }
            }

            public string Text { [UsedImplicitly] get; set; }

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