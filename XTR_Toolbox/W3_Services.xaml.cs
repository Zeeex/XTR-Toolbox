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
        private readonly List<ServiceItem> _servicesList = new List<ServiceItem>();
        private SortAdorner _listViewSortAdorner;

        private GridViewColumnHeader _listViewSortCol;
        private int _numRunning;

        public Window3()
        {
            InitializeComponent();
            PopulateServices();
            TbRunNum.Text = _numRunning.ToString();
            DataContext = new ServiceItem();
            LvServices.ItemsSource = _servicesList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvServices.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("Full", ListSortDirection.Ascending));
            view.Filter = UserFilter;
            TxtSearch.Focus();
        }

        private static string GetStartType(ServiceController service)
        {
            string startType = service.StartType.ToString();
            if (startType != "Automatic") return startType;
            using (RegistryKey delayedValue =
                Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\" + service.ServiceName))
            {
                string dAuto = delayedValue?.GetValue("DelayedAutostart", "").ToString();
                if (dAuto == "1")
                    startType += " (Delayed)";
            }

            return startType;
        }

        private void PopulateServices()
        {
            foreach (ServiceController service in ServiceController.GetServices())
            {
                string[] array = new string[4];
                try
                {
                    array[0] = service.ServiceName;
                    array[1] = service.DisplayName;
                    array[2] = service.Status.ToString() == "Stopped" ? "" : service.Status.ToString();
                    array[3] = GetStartType(service);
                    _servicesList.Add(new ServiceItem
                    {
                        Name = array[0],
                        Full = array[1],
                        Status = array[2],
                        Startup = array[3]
                    });
                    if (array[2] == "Running")
                        _numRunning++;
                }
                catch
                {
                    // ignored
                }
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
            string menuItem = ((MenuItem) sender).Name;
            foreach (ServiceItem serviceItem in LvServices.SelectedItems)
            {
                string serviceName = serviceItem.Name;
                switch (menuItem)
                {
                    case nameof(Start):
                        Shared.ServiceRestarter(serviceName, true);
                        _numRunning++;
                        TbRunNum.Text = _numRunning.ToString();
                        break;
                    case nameof(Stop):
                        Shared.ServiceRestarter(serviceName, false);
                        _numRunning--;
                        TbRunNum.Text = _numRunning.ToString();
                        break;
                    default:
                        string servType = null, delayed = null;
                        switch (menuItem)
                        {
                            case nameof(Disable):
                                servType = "4";
                                break;
                            case nameof(Manual):
                                servType = "3";
                                break;
                            case nameof(Auto):
                                servType = "2";
                                delayed = "0";
                                break;
                            case nameof(AutoDelayed):
                                servType = "2";
                                delayed = "1";
                                break;
                        }

                        Shared.ServiceStartTypeSet(serviceName, servType, delayed);
                        break;
                }

                ServiceController service = new ServiceController(serviceName);
                serviceItem.Status = service.Status.ToString() == "Stopped" ? "" : service.Status.ToString();
                serviceItem.Startup = GetStartType(service);
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