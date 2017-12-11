using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;

namespace XTR_Toolbox
{
    public partial class Window1
    {
        private static readonly List<CheckBoxCustom> AppsList = new List<CheckBoxCustom>();

        public Window1()
        {
            InitializeComponent();
            AppsList.Clear();
            FillApps();
            AppsListBox.ItemsSource = AppsList;
        }

        private static void FillApps()
        {
            string[] appList =
            {
                "Alarms & Clock",
                "App Connector",
                "App Installer",
                "Calendar and Mail",
                "Camera",
                "Feedback Hub",
                "Get Help",
                "Get Office",
                "Get Started",
                "Groove Music",
                "Maps",
                "Messaging",
                "Microsoft Wi-Fi",
                "Money",
                "Movies & TV",
                "News",
                "OneNote",
                "Paid Wi-Fi & Cellular",
                "Paint 3D",
                "People",
                "Phone",
                "Photos",
                "Print 3D",
                "Scan",
                "Skype Preview",
                "Solitaire Collection",
                "Sports",
                "Sticky Notes",
                "Sway",
                "Voice Recorder",
                "Weather",
                "Windows DVD Player",
                "Xbox"
            };
            foreach (string adder in appList)
                AppsList.Add(new CheckBoxCustom {Text = adder});
        }

        private void AppsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count != 0 || e.AddedItems.Count == 0) return;
            CheckBoxCustom t = (CheckBoxCustom) e.AddedItems[0];
            t.Checked = !t.Checked;
            AppsListBox.SelectedIndex = -1;
        }

        private void BtnProcessApps_Click(object sender, RoutedEventArgs e)
        {
            BtnDelApps.IsEnabled = BtnRepApps.IsEnabled = false;
            string psPath = Path.Combine(Path.GetTempPath(), "Manage_Win_Apps.ps1");
            try
            {
                using (StreamWriter printText = File.CreateText(psPath))
                {
                    if (Equals(sender, BtnDelApps))
                    {
                        // 33 WIN APPS
                        List<string> winAppsList = new List<string>
                        {
                            "*alarms*",
                            "*appconnector*",
                            "*appinstaller*",
                            "*communicationsapps*",
                            "*camera*",
                            "*feedback*",
                            "*gethelp*",
                            "*officehub*",
                            "*getstarted*",
                            "*zunemusic*",
                            "*maps*",
                            "*messaging*",
                            "*connectivitystore*",
                            "*bingfinance*",
                            "*zunevideo*",
                            "*bingnews*",
                            "*onenote*",
                            "*oneconnect*",
                            "*mspaint*",
                            "*people*",
                            "*phone*",
                            "*photos*",
                            "*print3d*",
                            "*windowsscan*",
                            "*skypeapp*",
                            "*solitaire*",
                            "*bingsports*",
                            "*sticky*",
                            "*sway*",
                            "*soundrecorder*",
                            "*bingweather*",
                            "*dvd*",
                            "*xbox*"
                        };
                        bool hasProc = false;
                        for (int index = 0; index < AppsList.Count; index++)
                        {
                            CheckBoxCustom item = AppsList[index];
                            if (!item.Checked) continue;
                            hasProc = true;
                            printText.WriteLine("get-appxpackage " + winAppsList[index] + " | remove-appxpackage");
                        }
                        if (hasProc == false)
                        {
                            MessageBox.Show("Select an app to uninstall first.");
                            return;
                        }
                    }
                    else if (Equals(sender, BtnRepApps))
                    {
                        printText.WriteLine(
                            @"Get-AppXPackage | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml""}");
                    }
                }
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Normal,
                    FileName = "powershell.exe",
                    Arguments = "\"{ Set-ExecutionPolicy Bypass }; clear; & '" + psPath + "'\""
                };
                using (Process process = new Process {StartInfo = startInfo})
                {
                    process.Start();
                    process.WaitForExit();
                }
            }
            finally
            {
                File.Delete(psPath);
                BtnDelApps.IsEnabled = BtnRepApps.IsEnabled = true;
            }
        }

        private void ChBoxAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBoxCustom t in AppsList)
                t.Checked = true;
        }

        private void ChBoxAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBoxCustom t in AppsListBox.Items)
                t.Checked = false;
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private class CheckBoxCustom : INotifyPropertyChanged
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
    }
}