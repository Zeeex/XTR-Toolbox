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
        private readonly List<ServiceModel> _servicesList = new List<ServiceModel>();
        private SortAdorner _listViewSortAdorner;

        private GridViewColumnHeader _listViewSortCol;
        private int _numRunning;

        public Window3()
        {
            InitializeComponent();
            PopulateServices();
            TbRunNum.Text = _numRunning.ToString();
            DataContext = new ServiceModel();
            LvServices.ItemsSource = _servicesList;
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(LvServices.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("Full", ListSortDirection.Ascending));
            view.Filter = UserFilter;
            TbSearch.Focus();
            Shared.SnackBarTip(MainSnackbar);
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
                    _servicesList.Add(new ServiceModel
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

        private void ServiceChange_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Command is RoutedUICommand eCommand)) return;
            foreach (ServiceModel item in LvServices.SelectedItems)
            {
                string serviceName = item.Name;
                if (eCommand == ServiceCmd.Start)
                {
                    Shared.ServiceRestarter(serviceName, true);
                    _numRunning++;
                    TbRunNum.Text = _numRunning.ToString();
                }
                else if (eCommand == ServiceCmd.Stop)
                {
                    Shared.ServiceRestarter(serviceName, false);
                    _numRunning--;
                    TbRunNum.Text = _numRunning.ToString();
                }
                else
                {
                    string startType = null, delayed = null;
                    if (eCommand == ServiceCmd.Disable)
                    {
                        startType = "4";
                    }
                    else if (eCommand == ServiceCmd.Manual)
                    {
                        startType = "3";
                    }
                    else if (eCommand == ServiceCmd.Automatic)
                    {
                        startType = "2";
                        delayed = "0";
                    }
                    else if (eCommand == ServiceCmd.AutoDelayed)
                    {
                        startType = "2";
                        delayed = "1";
                    }

                    Shared.ServiceStartType(serviceName, startType, delayed);
                }

                ServiceController service = new ServiceController(serviceName);
                item.Status = service.Status.ToString() == "Stopped" ? "" : service.Status.ToString();
                item.Startup = GetStartType(service);
            }
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(LvServices.ItemsSource).Refresh();

        private bool UserFilter(object item)
        {
            if (TbSearch.Text.Length == 0) return true;
            return ((ServiceModel) item).Full.IndexOf(TbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private class ServiceModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private string _startup;
            private string _status;
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

    public static class ServiceCmd
    {
        public static readonly RoutedUICommand Start = new RoutedUICommand("Start Service", "Start", typeof(ServiceCmd),
            new InputGestureCollection {new KeyGesture(Key.S, ModifierKeys.Control)});

        public static readonly RoutedUICommand Stop = new RoutedUICommand("Stop Service", "Stop", typeof(ServiceCmd),
            new InputGestureCollection {new KeyGesture(Key.D, ModifierKeys.Control)});

        public static readonly RoutedUICommand Disable = new RoutedUICommand("Disable", "Dis", typeof(ServiceCmd),
            new InputGestureCollection {new KeyGesture(Key.D1, ModifierKeys.Control)});

        public static readonly RoutedUICommand Manual = new RoutedUICommand("Manual", "Man", typeof(ServiceCmd),
            new InputGestureCollection {new KeyGesture(Key.D2, ModifierKeys.Control)});

        public static readonly RoutedUICommand Automatic = new RoutedUICommand("Automatic", "Auto", typeof(ServiceCmd),
            new InputGestureCollection {new KeyGesture(Key.D3, ModifierKeys.Control)});

        public static readonly RoutedUICommand AutoDelayed = new RoutedUICommand("Auto (Delayed)", "AutoDel",
            typeof(ServiceCmd),
            new InputGestureCollection {new KeyGesture(Key.D4, ModifierKeys.Control)});
    }
}