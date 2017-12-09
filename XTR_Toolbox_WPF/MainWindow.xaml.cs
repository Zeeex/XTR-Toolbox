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
            window.ShowDialog();
        }

        private void BtnIconCacheCleaner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // KILL CURRENT EXPLORER(S) =======
                foreach (Process processKill in Process.GetProcessesByName("explorer"))
                {
                    processKill.Kill();
                }
                // CLEANING CACHE =======
                ProcessStartInfo startInfo = new ProcessStartInfo()
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
        }

        private void BtnDWMResterter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Process proc = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo()
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
        }

        private void BtnEventsCleaner_Click(object sender, RoutedEventArgs e)
        {
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
                Process proc = new Process()
                {
                    StartInfo = startInfo
                };
                Button eventBtn = (Button) sender;
                eventBtn.IsEnabled = false;
                proc.Start();
                proc.WaitForExit();
                eventBtn.IsEnabled = true;
            }
            catch
            {
                //ignored
            }
            finally
            {
                File.Delete(batpath);
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e) =>
            Process.Start(e.Uri.AbsoluteUri);


        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (!Equals(w.DataContext, this))
                    w.Close();
            }
        }
    }
}