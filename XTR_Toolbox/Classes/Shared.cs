using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Icon = System.Drawing.Icon;

namespace XTR_Toolbox.Classes
{
    internal static class Shared
    {
        private static int _tipCount;

        internal static BitmapSource PathToIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;
            if (!File.Exists(filePath))
            {
                int commaIndex = filePath.LastIndexOf(",", filePath.Length - 2, StringComparison.Ordinal);

                if (commaIndex >= 0)
                    filePath = filePath.Substring(0, commaIndex);
            }

            if (!File.Exists(filePath))
                return null;
            using (Icon sysicon = Icon.ExtractAssociatedIcon(filePath))
            {
                return sysicon == null
                    ? null
                    : Imaging.CreateBitmapSourceFromHIcon(sysicon.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
            }
        }

        internal static bool ServiceRestarter(string name, bool isStart)
        {
            using (ServiceController service = new ServiceController(name))
            {
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
            }

            return false;
        }

        internal static void ServiceStartType(string serviceName, string startType, string delayed = null)
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

        internal static void ShowSnackBar(Snackbar sender)
        {
            _tipCount++;
            const string defMessage = "Tip: You can select multiple items by holding CTRL.";
            if (_tipCount < 5)
                Task.Run(() => { Thread.Sleep(100); }).ContinueWith(t => { sender.MessageQueue.Enqueue(defMessage); },
                    TaskScheduler.FromCurrentSynchronizationContext());
        }

        internal static RegistryHive StringToRegistryHive(string hive)
        {
            RegistryHive converted = RegistryHive.LocalMachine;
            if (hive == "HKEY_CURRENT_USER") converted = RegistryHive.CurrentUser;
            return converted;
        }

        internal static class FitWindow
        {
            private static readonly double SysHeight = SystemParameters.PrimaryScreenHeight;
            private static readonly double SysWidth = SystemParameters.PrimaryScreenWidth;

            internal static void Init(double w, double h)
            {
                if (SysWidth > w && SysHeight > h) return;
                Window currWin = Application.Current.Windows.OfType<Window>().Single(win => win.IsActive);
                if (currWin != null) currWin.WindowState = WindowState.Maximized;
            }
        }
    }
}