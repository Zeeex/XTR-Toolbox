using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Icon = System.Drawing.Icon;

namespace XTR_Toolbox
{
    internal static class Shared
    {
        private static int _tipCount;

        public static int StartProc(string file, string arg = "", ProcessWindowStyle style = ProcessWindowStyle.Normal,
            string exMsg = "", bool wait = true)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = arg,
                WindowStyle = style
            };
            using (Process proc = new Process())
            {
                try
                {
                    proc.StartInfo = startInfo;
                    proc.Start();
                    if (wait)
                        proc.WaitForExit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{exMsg}Error: {ex.Message}");
                }

                return proc.ExitCode;
            }
        }

        public static BitmapSource PathToIcon(string regIcon)
        {
            if (string.IsNullOrEmpty(regIcon))
                return null;
            if (!File.Exists(regIcon))
            {
                int commaIndex = regIcon.LastIndexOf(",", regIcon.Length - 2, StringComparison.Ordinal);
                if (commaIndex >= 0)
                    regIcon = regIcon.Substring(0, commaIndex);
            }

            if (!File.Exists(regIcon))
                return null;
            using (Icon sysicon = Icon.ExtractAssociatedIcon(regIcon))
            {
                return sysicon == null
                    ? null
                    : Imaging.CreateBitmapSourceFromHIcon(sysicon.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
            }
        }

        public static bool ServiceRestarter(string name, bool isStart)
        {
            ServiceController service = new ServiceController(name);
            try
            {
                if (service.Status.Equals(ServiceControllerStatus.Running) ||
                    service.Status.Equals(ServiceControllerStatus.StartPending))
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    if (!service.Status.Equals(ServiceControllerStatus.Stopped) &&
                        !service.Status.Equals(ServiceControllerStatus.StopPending))
                    {
                        MessageBox.Show($"Service: {service.DisplayName} can\'t be stopped.");
                        return false;
                    }
                }

                if (isStart)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    if (!service.Status.Equals(ServiceControllerStatus.Running) &&
                        !service.Status.Equals(ServiceControllerStatus.StartPending))
                    {
                        MessageBox.Show($"Service: {service.DisplayName} can\'t be started.");
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                //ignored
            }

            return false;
        }

        public static void ServiceStartType(string serviceName, string startType, string delayed = null)
        {
            try
            {
                using (RegistryKey servK =
                    Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, true))
                {
                    if (startType != null)
                        servK?.SetValue("Start", startType, RegistryValueKind.DWord);
                    if (delayed != null)
                        servK?.SetValue("DelayedAutostart", delayed, RegistryValueKind.DWord);
                }
            }
            catch
            {
                //ignored
            }
        }

        public static void SnackBarTip(Snackbar sender)
        {
            _tipCount++;
            if (_tipCount < 5)
                Task.Factory.StartNew(() => { Thread.Sleep(100); }).ContinueWith(
                    t => { sender.MessageQueue.Enqueue("Tip: You can select multiple items by holding CTRL."); },
                    TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}