using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace XTR_Toolbox
{
    public partial class Window3
    {
        private static readonly List<ServiceItem> ServicesList = new List<ServiceItem>();
        private SortAdorner _listViewSortAdorner;

        private GridViewColumnHeader _listViewSortCol;

        public Window3()
        {
            InitializeComponent();
            ServicesList.Clear();
            PopulateServices();
            DataContext = new ServiceItem();
            LvServices.ItemsSource = ServicesList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvServices.ItemsSource);
            view.Filter = UserFilter;
            TxtSearch.Focus();
        }

        private static string GetStartupValue(ServiceController service)
        {
            string tempStartup = service.StartType.ToString(); // .Net 4.6.1
            if (tempStartup != "Automatic") return tempStartup;
            using (RegistryKey delayedValue =
                Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\" + service.ServiceName))
            {
                object keyValue = delayedValue?.GetValue("DelayedAutostart");
                if (keyValue == null) return tempStartup;
                int.TryParse(keyValue.ToString(), out int delayed);
                if (delayed == 1)
                    tempStartup += " (Delayed)";
                delayedValue.Close();
            }
            return tempStartup;
        }

        private static void PopulateServices()
        {
            foreach (ServiceController service in ServiceController.GetServices())
            {
                string[] array = new string[4];
                try
                {
                    array[0] = service.ServiceName;
                    array[1] = service.DisplayName;
                    array[2] = service.Status.ToString() == "Stopped" ? "" : service.Status.ToString();
                    array[3] = GetStartupValue(service);
                }
                catch
                {
                    // ignored
                }
                ServicesList.Add(new ServiceItem
                {
                    Name = array[0],
                    Full = array[1],
                    Status = array[2],
                    Startup = array[3]
                });
            }
        }

        private static void ServiceRestarter(string serviceName, bool serviceRestart)
        {
            ServiceController serviceController = new ServiceController(serviceName);
            try
            {
                if (serviceController.Status.Equals(ServiceControllerStatus.Running) ||
                    serviceController.Status.Equals(ServiceControllerStatus.StartPending))
                {
                    serviceController.Stop();
                    serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(6));
                }
                if (serviceRestart)
                {
                    serviceController.Start();
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(6));
                }
            }
            catch
            {
                // ignored
            }
        }

        private void LvUsersColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = sender as GridViewColumnHeader;
            string sortBy = column?.Tag.ToString();
            if (_listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(_listViewSortCol).Remove(_listViewSortAdorner);
                LvServices.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (Equals(_listViewSortCol, column) && Equals(_listViewSortAdorner.Direction, newDir))
                newDir = ListSortDirection.Descending;

            _listViewSortCol = column;
            _listViewSortAdorner = new SortAdorner(_listViewSortCol, newDir);
            if (_listViewSortCol != null) AdornerLayer.GetAdornerLayer(_listViewSortCol).Add(_listViewSortAdorner);
            if (sortBy != null) LvServices.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServiceItem listItem in LvServices.SelectedItems)
            {
                string serviceName = listItem.Name;
                if (Equals(sender, Start))
                    ServiceRestarter(serviceName, true);
                else if (Equals(sender, Stop))
                    ServiceRestarter(serviceName, false);
                else
                    using (RegistryKey setStartValue =
                        Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\" + serviceName, true))
                    {
                        string startValue = null;
                        if (Equals(sender, Disable))
                        {
                            startValue = "4";
                        }
                        else if (Equals(sender, Manual))
                        {
                            startValue = "3";
                        }
                        else if (Equals(sender, Auto))
                        {
                            startValue = "2";
                        }
                        else if (Equals(sender, AutoDelayed))
                        {
                            startValue = "2";
                            setStartValue?.SetValue("DelayedAutostart", 1, RegistryValueKind.DWord);
                        }
                        if (startValue != null)
                            setStartValue?.SetValue("Start", startValue, RegistryValueKind.DWord);
                        setStartValue?.Close();
                    }
                ServiceController service = new ServiceController(serviceName);
                listItem.Status = service.Status.ToString() == "Stopped" ? "" : service.Status.ToString();
                listItem.Startup = GetStartupValue(service);
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(LvServices.ItemsSource).Refresh();

        private bool UserFilter(object item)
        {
            if (string.IsNullOrEmpty(TxtSearch.Text))
                return true;
            return ((ServiceItem) item).Full.IndexOf(TxtSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private class ServiceItem : INotifyPropertyChanged
        {
            private string _startup;
            private string _status;

            public event PropertyChangedEventHandler PropertyChanged;
            public string Full { [UsedImplicitly] get; set; }

            public string Name { [UsedImplicitly] get; set; }

            public string Startup
            {
                [UsedImplicitly] get => _startup;
                set
                {
                    if (_startup == value) return;
                    _startup = value;
                    NotifyPropertyChanged(nameof(Startup));
                }
            }


            public string Status
            {
                [UsedImplicitly] get => _status;
                set
                {
                    if (_status == value) return;
                    _status = value;
                    NotifyPropertyChanged(nameof(Status));
                }
            }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}