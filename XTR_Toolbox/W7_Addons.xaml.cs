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
using Newtonsoft.Json.Linq;

namespace XTR_Toolbox
{
    public partial class Window7
    {
        private readonly ObservableCollection<AddonItem> _addonList = new ObservableCollection<AddonItem>();

        private readonly string _chromeData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data\");

        private readonly List<string> _chromeProfileNames = new List<string>();
        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window7()
        {
            InitializeComponent();
            PopulateAddons();
            DataContext = new AddonItem();
            LvAddons.ItemsSource = _addonList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvAddons.ItemsSource);
            view.Filter = UserFilter;
            TbSearch.Focus();
            Shared.SnackBarTip(MainSnackbar);
        }

        private static bool DeleteAddon(string deletePath)
        {
            if (Process.GetProcessesByName("chrome").Length > 0)
            {
                MessageBoxResult mB = MessageBox.Show(
                    "Google Chrome is currently open, extensions can't be uninstalled until all processes are closed." +
                    "\n\nSAVE YOUR WORK BEFORE CONTINUING!" +
                    "\n\nDo you want to force close Chrome?",
                    "Close Google Chrome before continuing", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (mB == MessageBoxResult.No) return false;
                try
                {
                    foreach (Process processKill in Process.GetProcessesByName("chrome")) processKill.Kill();
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                Directory.Delete(deletePath, true);
            }
            catch
            {
                //ignored
            }

            return !Directory.Exists(deletePath);
        }

        private void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            for (int index = LvAddons.SelectedItems.Count - 1; index >= 0; index--)
            {
                AddonItem listItem = (AddonItem) LvAddons.SelectedItems[index];
                if (DeleteAddon(Path.Combine(listItem.Path, listItem.Id)))
                    _addonList.Remove(listItem);
            }
        }

        private IEnumerable<string> GetProfileNames()
        {
            string localJsonChrome = Path.Combine(_chromeData, "Local State");
            JEnumerable<JToken> tokenEnumProfile =
                JObject.Parse(File.ReadAllText(localJsonChrome))["profile"]["info_cache"].Children();
            List<string> profileList = tokenEnumProfile
                .Select(item => item.Path.Substring(item.Path.LastIndexOf(".", StringComparison.Ordinal) + 1)).ToList();
            return profileList;
        }

        private void LvUsersColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = sender as GridViewColumnHeader;
            string sortBy = column?.Tag.ToString();
            if (_listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Remove(_listViewSortAdorner);
                LvAddons.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (Equals(_listViewSortCol, column) && Equals(_listViewSortAdorner.Direction, newDir))
                newDir = ListSortDirection.Descending;

            _listViewSortCol = column;
            _listViewSortAdorner = new SortAdorner(_listViewSortCol, newDir);
            if (_listViewSortCol != null) AdornerLayer.GetAdornerLayer(_listViewSortCol).Add(_listViewSortAdorner);
            if (sortBy != null) LvAddons.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void PopulateAddons()
        {
            _chromeProfileNames.AddRange(GetProfileNames());
            foreach (string prof in _chromeProfileNames)
            {
                string chromeAddonDir = Path.Combine(_chromeData, prof, "Extensions");
                if (!Directory.Exists(chromeAddonDir))
                {
                    MessageBox.Show("Error scanning for extensions.");
                    return;
                }

                string[] addonsIdDirs = Directory.GetDirectories(chromeAddonDir);

                string secureJsonChrome = Path.Combine(_chromeData, prof, "Secure Preferences");
                if (!File.Exists(secureJsonChrome)) return;
                JObject o = JObject.Parse(File.ReadAllText(secureJsonChrome));
                JToken ids = o["extensions"]["settings"];

                foreach (JToken i in ids)
                {
                    string addonId = i.Path.Substring(i.Path.LastIndexOf(".", StringComparison.Ordinal) + 1);
                    if (!addonsIdDirs.Any(m => m.Contains(addonId))) continue;
                    try
                    {
                        string addonName = (string) i.First["manifest"]["name"];
                        _addonList.Add(new AddonItem
                        {
                            Id = addonId,
                            Name = addonName,
                            Path = chromeAddonDir
                        });
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(LvAddons.ItemsSource).Refresh();

        private bool UserFilter(object item)
        {
            if (TbSearch.Text.Length == 0) return true;
            return ((AddonItem) item).Name.IndexOf(TbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_chromeData)) Close();
        }

        private class AddonItem
        {
            public string Id { [UsedImplicitly] get; set; }
            public string Name { [UsedImplicitly] get; set; }
            public string Path { [UsedImplicitly] get; set; }
        }
    }

    public static class BrowserCmd
    {
        public static readonly RoutedUICommand Delete = new RoutedUICommand("Delete", "Del", typeof(BrowserCmd),
            new InputGestureCollection {new KeyGesture(Key.Delete, ModifierKeys.Control)});
    }
}