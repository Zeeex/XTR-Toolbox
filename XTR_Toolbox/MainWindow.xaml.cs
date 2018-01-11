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
            BtnChrome.IsEnabled = ChromeBtnState();
        }

        private static bool ChromeBtnState()
        {
            try
            {
                return Directory.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Google\Chrome\User Data\"));
            }
            catch
            {
                return false;
            }
        }

        private void BtnMultiWindowOpener(object sender, RoutedEventArgs e)
        {
            Window w = new Window();
            if (Equals(sender, BtnWinApps))
                w = new Window1();
            else if (Equals(sender, BtnAutoruns))
                w = new Window2();
            else if (Equals(sender, BtnServices))
                w = new Window3();
            else if (Equals(sender, BtnHostsEditor))
                w = new Window4();
            else if (Equals(sender, BtnCleaner))
                w = new Window5();
            else if (Equals(sender, BtnSoftware))
                w = new Window6();
            else if (Equals(sender, BtnChrome))
                w = new Window7();
            w.ShowDialog();
        }

        private void BtnIconRebuild_Click(object sender, RoutedEventArgs e)
        {
            Button btnIcoReb = (Button) sender;
            btnIcoReb.IsEnabled = false;
            try
            {
                string env = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (env == "") return;
                foreach (Process proc in Process.GetProcessesByName("explorer")) proc.Kill();

                try
                {
                    File.Delete(Path.Combine(env, "IconCache.db"));
                }
                catch
                {
                    //ignored
                }

                foreach (string f in Directory.GetFiles(
                    Path.Combine(env, @"Microsoft\Windows\Explorer"), "iconcache*"))
                    try
                    {
                        File.Delete(f);
                    }
                    catch
                    {
                        //ignored
                    }

                if (Process.GetProcessesByName("explorer").Length == 0) Shared.StartProc("explorer.exe");
            }
            catch
            {
                //ignored
            }
            finally
            {
                btnIcoReb.IsEnabled = true;
            }
        }

        private void BtnFontRebuild_Click(object sender, RoutedEventArgs e)
        {
            Button btnFontReb = (Button) sender;
            btnFontReb.IsEnabled = false;
            const string servName = "FontCache";
            try
            {
                string env = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                Shared.ServiceStartTypeSet(servName, "4");
                Shared.ServiceRestarter(servName, false);
                Shared.StartProc("cmd.exe",
                    @"/C icacls ""%WinDir%\ServiceProfiles\LocalService"" /grant ""%UserName%"":F /C /T /Q",
                    ProcessWindowStyle.Hidden);
                string font1 = Path.Combine(env, @"System32\FNTCACHE.DAT");
                try
                {
                    File.Delete(font1);
                }
                catch
                {
                    //ignored
                }

                foreach (string f in Directory.GetFiles(
                    Path.Combine(env, @"ServiceProfiles\LocalService\AppData\Local\FontCache"), "*FontCache*"))
                    try
                    {
                        File.Delete(f);
                    }
                    catch
                    {
                        //ignored
                    }
            }
            catch
            {
                //ignored
            }

            Shared.ServiceStartTypeSet(servName, "2", "0");
            Shared.ServiceRestarter(servName, true);
            btnFontReb.IsEnabled = true;
        }


        private void BtnEventsCleaner_Click(object sender, RoutedEventArgs e)
        {
            Button btnEvent = (Button) sender;
            btnEvent.IsEnabled = false;
            string batPath = Path.Combine(Path.GetTempPath(), "Clean_EventLogs.bat");
            if (File.Exists(batPath))
                File.Delete(batPath);
            using (StreamWriter sw = File.CreateText(batPath))
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

            Shared.StartProc(batPath, exMsg: "There was an error clearing event logs.\n");
            File.Delete(batPath);
            btnEvent.IsEnabled = true;
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
                "This will remove Windows Updates related to telemetry in Windows 7 and 8.1. This has no effect on Windows 10. It's safe to run. \n\nUpdates to uninstall:\n" +
                string.Join("\n", updates) +
                "\n\nAre you sure you want to do this?",
                "Uninstall Telemetry Updates", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (mB == MessageBoxResult.No)
            {
                btnTelemetry.IsEnabled = true;
                return;
            }

            string batPath = Path.Combine(Path.GetTempPath(), "Uninstall_Telemetry_Updates.bat");
            if (File.Exists(batPath))
                File.Delete(batPath);
            using (StreamWriter sw = File.CreateText(batPath))
            {
                sw.WriteLine("@echo off");
                foreach (string up in updates)
                    sw.WriteLine("start /w wusa.exe /uninstall /kb:" + up.Replace("KB", "") + " /quiet /norestart");

                sw.WriteLine("exit");
            }

            Shared.StartProc(batPath, exMsg: "There was an error uninstalling telemetry updates.\n");
            File.Delete(batPath);
            btnTelemetry.IsEnabled = true;
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e) =>
            Process.Start(e.Uri.AbsoluteUri);

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();
    }
}