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
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using XTR_Toolbox.Classes;
using XTR_Toolbox.Dialogs;

namespace XTR_Toolbox
{
    public partial class Window7
    {
        public static readonly string ChromeDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data\");

        private readonly ObservableCollection<AddonModel> _addonList = new ObservableCollection<AddonModel>();

        private readonly List<string> _chromeProfileNames = new List<string>();
        private SortAdorner _listViewSortAdorner;
        private GridViewColumnHeader _listViewSortCol;

        public Window7()
        {
            InitializeComponent();
            PopulateAddons();
            DataContext = new AddonModel();
            LvAddons.ItemsSource = _addonList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvAddons.ItemsSource);
            view.Filter = UserFilter;
            TbSearch.Focus();
            Shared.ShowSnackBar(MainSnackbar);
        }

        private static IEnumerable<string> GetProfileNames()
        {
            string localJsonChrome = Path.Combine(ChromeDataPath, "Local State");
            JEnumerable<JToken> tokenEnumProfile =
                JObject.Parse(File.ReadAllText(localJsonChrome))["profile"]["info_cache"].Children();
            List<string> profileList = tokenEnumProfile
                .Select(item => item.Path.Substring(item.Path.LastIndexOf(".", StringComparison.Ordinal) + 1)).ToList();
            return profileList;
        }

        private async void DeleteCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int selCount = LvAddons.SelectedItems.Count;
            if (selCount > 20)
            {
                object result = await DialogHost.Show(
                    new GenYesNoUc($"Are you sure you want to delete {selCount} items?"), "RootDialog");
                if (result == null || result.ToString() != "Y") return;
            }

            if (Process.GetProcessesByName("chrome").Length > 0)
            {
                object result = await DialogHost.Show(new ChromeProcessUc(), "RootDialog");
                if (result == null || result.ToString() != "Y") return;
                CustomProc.KillAllProc("chrome");
            }

            for (int index = selCount - 1; index >= 0; index--)
            {
                AddonModel item = (AddonModel) LvAddons.SelectedItems[index];
                try
                {
                    Directory.Delete(Path.Combine(item.Path, item.Id), true);
                    _addonList.Remove(item);
                }
                catch
                {
                    //ignored
                }
            }
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
                string chromeAddonDir = Path.Combine(ChromeDataPath, prof, "Extensions");
                if (!Directory.Exists(chromeAddonDir))
                {
                    MessageBox.Show("Error scanning for extensions.");
                    return;
                }

                string[] addonsIdDirs = Directory.GetDirectories(chromeAddonDir);

                string secureJsonChrome = Path.Combine(ChromeDataPath, prof, "Secure Preferences");
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
                        _addonList.Add(new AddonModel
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
            return ((AddonModel) item).Name.IndexOf(TbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(ChromeDataPath)) Close();
        }

        private class AddonModel
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