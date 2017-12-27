using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace XTR_Toolbox
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            if (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 2)
                BtnWinApps.IsEnabled = false; // DISABLED FOR WIN7
        }

        private void BtnMultiWindowOpener(object sender, RoutedEventArgs e)
        {
            Window window = new Window();
            if (Equals(sender, BtnWinApps))
                window = new Window1();
            else if (Equals(sender, BtnAutoruns))
                window = new Window2();
            else if (Equals(sender, BtnServices))
                window = new Window3();
            else if (Equals(sender, BtnHostsEditor))
                window = new Window4();
            else if (Equals(sender, BtnCleaner))
                window = new Window5();
            else if (Equals(sender, BtnSoftware))
                window = new Window6();
            else if (Equals(sender, BtnChrome))
                window = new Window7();
            try
            {
                window.ShowDialog();
            }
            catch
            {
                //ignored
            }
        }

        private void BtnIconCacheCleaner_Click(object sender, RoutedEventArgs e)
        {
            Button btnIconCleaner = (Button) sender;
            btnIconCleaner.IsEnabled = false;
            try
            {
                // KILL CURRENT EXPLORER(S) =======
                foreach (Process processKill in Process.GetProcessesByName("explorer"))
                {
                    processKill.Kill();
                }
                // CLEANING CACHE =======
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = @"/C DEL %userprofile%\AppData\Local\IconCache.db && " +
                                @"DEL %userprofile%\AppData\Local\Microsoft\Windows\Explorer\iconcache_*"
                };
                using (Process proc = new Process())
                {
                    proc.StartInfo = startInfo;
                    proc.Start();
                    // EXPLORER START =======
                    Process[] runningProcessByName = Process.GetProcessesByName("explorer");
                    if (runningProcessByName.Length == 0)
                    {
                        startInfo.FileName = "explorer.exe";
                        startInfo.Arguments = "";
                        proc.StartInfo = startInfo;
                        proc.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error clearing Icon Cache: " + ex.Message);
            }
            finally
            {
                btnIconCleaner.IsEnabled = true;
            }
        }

        private void BtnDWMResterter_Click(object sender, RoutedEventArgs e)
        {
            Button btnDwm = (Button) sender;
            btnDwm.IsEnabled = false;
            try
            {
                using (Process proc = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "cmd.exe",
                        Arguments = "/C taskkill /f /im dwm.exe && " +
                                    "net stop uxsms && net start uxsms"
                    };
                    proc.StartInfo = startInfo;
                    proc.Start();
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error restarting DWM: " + ex.Message);
            }
            finally
            {
                btnDwm.IsEnabled = true;
            }
        }

        private void BtnEventsCleaner_Click(object sender, RoutedEventArgs e)
        {
            Button btnEvent = (Button) sender;
            btnEvent.IsEnabled = false;
            string batpath = $@"{Environment.GetEnvironmentVariable("temp")}\Clean_EventLogs.bat";
            if (File.Exists(batpath))
                File.Delete(batpath);
            try
            {
                using (StreamWriter sw = File.CreateText(batpath))
                {
                    sw.WriteLine("@echo off");
                    sw.WriteLine("FOR /F \"tokens=1,2*\" %%V IN ('bcdedit') DO SET adminTest=%%V");
                    sw.WriteLine("IF (%adminTest%)==(Access) goto noAdmin");
                    sw.WriteLine("for /F \"tokens=*\" %%G in ('wevtutil.exe el') DO (call :do_clear \"%%G\")");
                    sw.WriteLine("echo.");
                    sw.WriteLine("echo goto theEnd");
                    sw.WriteLine(":do_clear");
                    sw.WriteLine("echo clearing %1");
                    sw.WriteLine("wevtutil.exe cl %1");
                    sw.WriteLine("goto :eof");
                    sw.WriteLine(":noAdmin");
                    sw.WriteLine("exit");
                }
                ProcessStartInfo startInfo = new ProcessStartInfo(batpath);
                Process proc = new Process
                {
                    StartInfo = startInfo
                };
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error clearing event logs: " + ex.Message);
            }
            finally
            {
                File.Delete(batpath);
                btnEvent.IsEnabled = true;
            }
        }

        private void BtnRemoveTelemetry_Click(object sender, RoutedEventArgs e)
        {
            Button btnTelemetry = (Button) sender;
            btnTelemetry.IsEnabled = false;
            string[] updates =
            {
                "KB2976978",
                "KB3075249",
                "KB3080149",
                "KB3021917",
                "KB3022345",
                "KB3068708",
                "KB3044374",
                "KB3035583",
                "KB2990214",
                "KB2952664",
                "KB3075853",
                "KB3065987",
                "KB3050265",
                "KB3075851",
                "KB2902907"
            };
            MessageBoxResult mB = MessageBox.Show(
                "This will remove Windows Updates related to telemetry in Windows 7 and 8.1. \n\nUpdates to uninstall:\n" +
                string.Join("\n", updates) +
                "\n\nAre you sure you want to do this?",
                "Uninstall Telemetry Updates", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (mB == MessageBoxResult.No)
            {
                btnTelemetry.IsEnabled = true;
                return;
            }
            string batpath = $@"{Environment.GetEnvironmentVariable("temp")}\Uninstall_Telemetry_Updates.bat";
            if (File.Exists(batpath))
                File.Delete(batpath);
            try
            {
                using (StreamWriter sw = File.CreateText(batpath))
                {
                    sw.WriteLine("@echo off");
                    foreach (string up in updates)
                    {
                        sw.WriteLine("start /w wusa.exe /uninstall /kb:" + up.Replace("KB", "") + " /quiet /norestart");
                    }
                    sw.WriteLine("exit");
                }
                ProcessStartInfo startInfo = new ProcessStartInfo(batpath);
                Process proc = new Process
                {
                    StartInfo = startInfo
                };
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error uninstalling telemetry updates: " + ex.Message);
            }
            finally
            {
                File.Delete(batpath);
                btnTelemetry.IsEnabled = true;
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e) =>
            Process.Start(e.Uri.AbsoluteUri);
    }
}