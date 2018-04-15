using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using XTR_Toolbox.Classes;

namespace XTR_Toolbox.Dialogs
{
    public partial class QuickToolsUc
    {
        private readonly IndicatorModel _indicatorBind = new IndicatorModel();

        public QuickToolsUc()
        {
            InitializeComponent();
            DataContext = _indicatorBind;
        }

        private static void FontRebuild(IProgress<int> progress)
        {
            int c = 0;
            progress.Report(c += 25);
            const string servName = "FontCache";
            try
            {
                string env = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                Shared.ServiceStartType(servName, "4");
                Shared.ServiceRestarter(servName, false);
                progress.Report(c += 25);
                CustomProc.StartProc("cmd.exe",
                    @"/C icacls ""%WinDir%\ServiceProfiles\LocalService"" /grant ""%UserName%"":F /C /T /Q",
                    ProcessWindowStyle.Hidden);
                string font1 = Path.Combine(env, @"System32\FNTCACHE.DAT");
                progress.Report(c += 25);
                try
                {
                    File.Delete(font1);
                }
                catch
                {
                    //ignored
                }

                progress.Report(c);

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
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                Shared.ServiceStartType(servName, "2", "0");
                Shared.ServiceRestarter(servName, true);
                progress.Report(100);
            }
        }

        private static void IconRebuild(IProgress<int> progress)
        {
            int c = 0;
            progress.Report(c += 25);
            try
            {
                string env = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (env.Length == 0) return;
                foreach (Process proc in Process.GetProcessesByName("explorer")) proc.Kill();
                progress.Report(c += 25);
                try
                {
                    File.Delete(Path.Combine(env, "IconCache.db"));
                }
                catch
                {
                    //ignored
                }

                progress.Report(c += 25);

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

                progress.Report(c);
                if (Process.GetProcessesByName("explorer").Length == 0) CustomProc.StartProc("explorer.exe");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                progress.Report(100);
            }
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

            CustomProc.StartProc(batPath, exMsg: "There was an error clearing event logs.\n");
            File.Delete(batPath);
            btnEvent.IsEnabled = true;
        }

        private async void BtnFontRebuild_Click(object sender, RoutedEventArgs e)
        {
            int pb = _indicatorBind.FontProgress;
            if (pb != 0 && pb != 100) return;
            Progress<int> progress = new Progress<int>(s => _indicatorBind.FontProgress = s);
            await Task.Run(() => FontRebuild(progress));
        }

        private async void BtnIconRebuild_Click(object sender, RoutedEventArgs e)
        {
            int pb = _indicatorBind.IconProgress;
            if (pb != 0 && pb != 100) return;
            Progress<int> progress = new Progress<int>(s => _indicatorBind.IconProgress = s);
            await Task.Run(() => IconRebuild(progress));
        }

        private class IndicatorModel : INotifyPropertyChanged
        {
            private int _fontProgress;
            private int _iconProgress;

            public event PropertyChangedEventHandler PropertyChanged;

            public int FontProgress
            {
                [UsedImplicitly] get => _fontProgress;
                set => SetField(ref _fontProgress, value);
            }

            public int IconProgress
            {
                [UsedImplicitly] get => _iconProgress;
                set => SetField(ref _iconProgress, value);
            }

            private void SetField<T>(ref T field, T value, [CallerMemberName] string propName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}