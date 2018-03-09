using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using XTR_Toolbox.Classes;

namespace XTR_Toolbox
{
    public partial class Window1
    {
        private static readonly IReadOnlyDictionary<string, string> AppsList = new Dictionary<string, string>
        {
            {"Alarms & Clock", "*alarms*"},
            {"App Connector", "*appconnector*"},
            {"App Installer", "*appinstaller*"},
            {"Calendar and Mail", "*communicationsapps*"},
            {"Camera", "*camera*"},
            {"Feedback Hub", "*feedback*"},
            {"Get Help", "*gethelp*"},
            {"Get Office", "*officehub*"},
            {"Get Started", "*getstarted*"},
            {"Groove Music", "*zunemusic*"},
            {"Maps", "*maps*"},
            {"Messaging", "*messaging*"},
            {"Microsoft Wi-Fi", "*connectivitystore*"},
            {"Money", "*bingfinance*"},
            {"Movies & TV", "*zunevideo*"},
            {"News", "*bingnews*"},
            {"OneNote", "*onenote*"},
            {"Paid Wi-Fi & Cellular", "*oneconnect*"},
            {"Paint 3D", "*mspaint*"},
            {"People", "*people*"},
            {"Phone", "*phone*"},
            {"Photos", "*photos*"},
            {"Print 3D", "*print3d*"},
            {"Scan", "*windowsscan*"},
            {"Skype Preview", "*skypeapp*"},
            {"Solitaire Collection", "*solitaire*"},
            {"Sports", "*bingsports*"},
            {"Sticky Notes", "*sticky*"},
            {"Sway", "*sway*"},
            {"Voice Recorder", "*soundrecorder*"},
            {"Weather", "*bingweather*"},
            {"Windows DVD Player", "*dvd*"},
            {"Xbox", "*xbox*"}
        };

        private readonly List<AppsModel> _appsModelList = new List<AppsModel>();

        public Window1()
        {
            InitializeComponent();
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

        private void FillApps()
        {
            foreach (KeyValuePair<string, string> kvp in AppsList)
                _appsModelList.Add(new AppsModel {CleanName = kvp.Key, ScriptName = kvp.Value});
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void ProcessAppsCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedCommand eCommand)) return;
            bool delCmd = eCommand == WinAppsCmd.DeleteApps;
            StackPanelBtns.IsEnabled = false;
            string psPath = Path.Combine(Path.GetTempPath(), "Win_Apps_Worker.ps1");
            try
            {
                using (StreamWriter printText = File.CreateText(psPath))
                {
                    if (delCmd)
                        foreach (AppsModel t in _appsModelList)
                        {
                            if (!t.Checked) continue;
                            printText.WriteLine("get-appxpackage " + t.ScriptName + " | remove-appxpackage");
                        }
                    else
                        printText.WriteLine(
                            @"Get-AppXPackage | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register """ +
                            @"$($_.InstallLocation)\AppXManifest.xml""}");
                }

                CustomProc.StartProc("powershell.exe", "\"{ Set-ExecutionPolicy Bypass }; clear; & '" + psPath + "'\"");
                File.Delete(psPath);
            }
            finally
            {
                StackPanelBtns.IsEnabled = true;
            }
        }

        private void UninstallCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = LbApps.Items.Cast<AppsModel>().Any(item => item.Checked);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FillApps();
            LbApps.ItemsSource = _appsModelList;
        }

        private class AppsModel : INotifyPropertyChanged
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

            public string CleanName { [UsedImplicitly] get; set; }
            public string ScriptName { [UsedImplicitly] get; set; }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
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