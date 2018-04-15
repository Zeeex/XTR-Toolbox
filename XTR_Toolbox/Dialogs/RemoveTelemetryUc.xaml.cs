using System.ComponentModel;
using System.IO;
using System.Windows;
using JetBrains.Annotations;
using XTR_Toolbox.Classes;

namespace XTR_Toolbox.Dialogs
{
    public partial class RemoveTelemetryUc
    {
        private readonly TextModel _textBind = new TextModel();

        private readonly string[] _updates =
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

        private string _telemetryText;

        public RemoveTelemetryUc()
        {
            InitializeComponent();
            TelemetryDialogText();
        }

        private void BtnTelemetryYes_Click(object sender, RoutedEventArgs e)
        {
            string batPath = Path.Combine(Path.GetTempPath(), "Uninstall_Telemetry_Updates.bat");
            if (File.Exists(batPath))
                File.Delete(batPath);
            using (StreamWriter sw = File.CreateText(batPath))
            {
                sw.WriteLine("@echo off");
                foreach (string up in _updates)
                    sw.WriteLine("start /w wusa.exe /uninstall /kb:" + up.Replace("KB", "") + " /quiet /norestart");

                sw.WriteLine("exit");
            }

            CustomProc.StartProc(batPath, exMsg: "There was an error uninstalling telemetry updates.\n");
            File.Delete(batPath);
        }

        private void TelemetryDialogText()
        {
            _telemetryText =
                "This will remove Windows Updates related to telemetry in Windows 7 and 8.1. \nThis has no effect on Windows 10. It's safe to run. \n\nUpdates to uninstall:\n" +
                string.Join("\n", _updates) +
                "\n\nAre you sure you want to do this?";
            TbTelemetry.DataContext = _textBind;
            _textBind.TelemetryText = _telemetryText;
        }

        private class TextModel : INotifyPropertyChanged
        {
            private string _telText;

            public event PropertyChangedEventHandler PropertyChanged;

            public string TelemetryText
            {
                [UsedImplicitly] get => _telText;

                set
                {
                    if (_telText == value) return;
                    _telText = value;
                    NotifyPropertyChanged(nameof(TelemetryText));
                }
            }

            private void NotifyPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}