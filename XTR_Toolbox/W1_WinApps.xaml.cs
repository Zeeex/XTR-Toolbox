using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using JetBrains.Annotations;
using XTR_Toolbox.Classes;

namespace XTR_Toolbox
{
    public partial class Window1
    {
        private static readonly PackageManager PacManager = new PackageManager();
        private ObservableCollection<AppsModel> _appsModelList = new ObservableCollection<AppsModel>();

        public Window1()
        {
            InitializeComponent();
        }

        private static IEnumerable<AppsModel> GetValidApps()
        {
            List<AppsModel> apps = new List<AppsModel>();
            foreach (Package p in PacManager.FindPackagesForUser(""))
                try
                {
                    if (p.IsFramework || !p.InstalledLocation.Path.Contains(@"\WindowsApps\")) continue;
                    string appName = PrettyName(p.Id.Name);
                    string appScriptName = p.Id.FullName;
                    apps.Add(new AppsModel {Name = appName, ScriptName = appScriptName});
                }
                catch (FileNotFoundException)
                {
                }

            return apps;
        }

        private static string PrettyName(string p)
        {
            const string ms = "Microsoft.";
            p = p.Substring(p.IndexOf(ms, StringComparison.OrdinalIgnoreCase) + ms.Length - 1).Replace(".", "");
            return string.Concat(p.Select((x, i) =>
                i > 1 && i < p.Length - 1 && (!char.IsLower(x) && char.IsLower(p[i - 1]) ||
                                              !char.IsLower(x) && char.IsLower(p[i + 1]))
                    ? $" {x}"
                    : x.ToString()));
        }

        private void AppsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count != 0 || e.AddedItems.Count == 0) return;
            AppsModel t = (AppsModel) e.AddedItems[0];
            t.Checked = !t.Checked;
            LbApps.SelectedIndex = -1;
        }

        private void ChBoxAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (AppsModel t in _appsModelList)
                t.Checked = true;
        }

        private void ChBoxAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (AppsModel t in _appsModelList)
                t.Checked = false;
        }

        private void DeleteAppsCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            StackPanelBtns.IsEnabled = false;
            for (int index = _appsModelList.Count - 1; index >= 0; index--)
            {
                AppsModel t = _appsModelList[index];
                if (!t.Checked) continue;
                IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> depOpe =
                    PacManager.RemovePackageAsync(t.ScriptName);
                ManualResetEvent opCompletedEvent = new ManualResetEvent(false);
                depOpe.Completed = (depProgress, status) => { opCompletedEvent.Set(); };
                opCompletedEvent.WaitOne();
                switch (depOpe.Status)
                {
                    case AsyncStatus.Error:
                        MessageBox.Show($"Error code: {depOpe.ErrorCode}\n" +
                                        $"Error text: {depOpe.GetResults().ErrorText}");
                        break;
                    case AsyncStatus.Canceled:
                        MessageBox.Show(@"Removal canceled");
                        break;
                    case AsyncStatus.Completed:
                    case AsyncStatus.Started:
                        _appsModelList.Remove(t);
                        break;
                    default:
                        MessageBox.Show($"{t.Name} removal failed");
                        break;
                }
            }

            StackPanelBtns.IsEnabled = true;
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void RestoreAppsCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            StackPanelBtns.IsEnabled = false;
            string psPath = Path.Combine(Path.GetTempPath(), "Win_Apps_Worker.ps1");
            try
            {
                using (StreamWriter fText = File.CreateText(psPath))
                {
                    string restoreApps =
                        $@"Get-AppXPackage -AllUsers | Foreach {{Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml""}}";
                    fText.WriteLine(restoreApps);
                }

                CustomProc.StartProc("powershell.exe", "\"{ Set-ExecutionPolicy Bypass }; clear; & '" + psPath + "'\"");
                File.Delete(psPath);
            }
            finally
            {
                StackPanelBtns.IsEnabled = true;
            }
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(LbApps.ItemsSource).Refresh();

        private void UninstallCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LbApps.Items.Cast<AppsModel>().Any(item => item.Checked);

        private bool UserFilter(object item)
        {
            if (TbSearch.Text.Length == 0) return true;
            return ((AppsModel) item).Name.IndexOf(TbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            Shared.FitWindow.Init(Width, Height);
            _appsModelList = new ObservableCollection<AppsModel>(GetValidApps());
            LbApps.ItemsSource = _appsModelList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LbApps.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            view.Filter = UserFilter;
        }

        private class AppsModel : INotifyPropertyChanged
        {
            private bool _checked;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool Checked
            {
                [UsedImplicitly] get => _checked;
                set => SetField(ref _checked, value);
            }

            public string Name { [UsedImplicitly] get; set; }
            public string ScriptName { [UsedImplicitly] get; set; }

            private void SetField(ref bool field, bool value, [CallerMemberName] string propName = null)
            {
                if (field == value) return;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
    }

    public static class WinAppsCmd
    {
        public static readonly RoutedCommand DeleteApps = new RoutedCommand("DelApps",
            typeof(WinAppsCmd), new InputGestureCollection {new KeyGesture(Key.D, ModifierKeys.Control)});

        public static readonly RoutedCommand ReturnApps = new RoutedCommand("RepApps",
            typeof(WinAppsCmd), new InputGestureCollection {new KeyGesture(Key.R, ModifierKeys.Control)});
    }
}