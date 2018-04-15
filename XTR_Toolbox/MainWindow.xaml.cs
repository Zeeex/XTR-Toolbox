using System;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using MaterialDesignThemes.Wpf;
using XTR_Toolbox.Dialogs;

namespace XTR_Toolbox
{
    public partial class MainWindow
    {
        public const string XtrVer = "2.1";
        private readonly HttpClient _cl = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            Title += XtrVer;
        }

        private async void Btn_QuickTools(object sender, RoutedEventArgs e) =>
            await DialogHost.Show(new QuickToolsUc(), "MainDialog");

        private async void Btn_Telemetry(object sender, RoutedEventArgs e) =>
            await DialogHost.Show(new RemoveTelemetryUc(), "MainDialog");

        private void BtnWinOpener(object sender, RoutedEventArgs e)
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
            Hide();
            w.ShowDialog();
            Show();
        }

        private void DarkCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PaletteHelper pH = new PaletteHelper();
            string curPal = pH.QueryPalette().PrimarySwatch.Name;
            if (string.Equals(curPal, "Brown", StringComparison.OrdinalIgnoreCase))
            {
                pH.ReplacePrimaryColor("LightGreen");
                pH.SetLightDark(true);
            }
            else
            {
                pH.ReplacePrimaryColor("Brown");
                pH.SetLightDark(false);
            }
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e) =>
            Process.Start(e.Uri.AbsoluteUri);

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

        private async void UpdateCheckAsync()
        {
            try
            {
                string res =
                    await _cl.GetStringAsync(
                        "https://gist.githubusercontent.com/Zeeex/33dc2b1bda3a4055a5bd293c4e425473/raw/");
                if (string.CompareOrdinal(XtrVer, res) < 0)
                    Title += @" (Latest: " + res + @")";
            }
            catch
            {
                // OFFLINE
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 2)
                BtnWinApps.IsEnabled = false; // DISABLED FOR WIN7
            BtnChrome.IsEnabled = Window7.ChromeExists();
            UpdateCheckAsync();
        }
    }

    public static class MainCmd
    {
        public static readonly RoutedCommand Dark = new RoutedCommand("Dark",
            typeof(MainCmd), new InputGestureCollection {new KeyGesture(Key.N, ModifierKeys.Control)});
    }
}