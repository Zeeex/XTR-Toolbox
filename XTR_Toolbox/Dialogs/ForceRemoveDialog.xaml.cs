using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace XTR_Toolbox.Dialogs
{
    public partial class ForceRemoveDialog
    {
        public static bool ForceOk;
        private readonly string[] _dirs;
        private readonly string[] _regs;
        private bool _sizeChanged;

        public ForceRemoveDialog(IEnumerable<string> dirs, IEnumerable<string> regs)
        {
            InitializeComponent();
            _dirs = dirs.ToArray();
            _regs = regs.ToArray();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ForceOk = false;
            Close();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            ForceOk = true;
            Close();
        }

        private bool CenterOwner()
        {
            if (Owner == null) return true;
            double top = Owner.Top + (Owner.Height - ActualHeight) / 2;
            double left = Owner.Left + (Owner.Width - ActualWidth) / 2;
            Top = top < 0 ? 0 : top;
            Left = left < 0 ? 0 : left;
            return true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_dirs.Length != 0)
            {
                TvDir.ItemsSource = _dirs;
                TvDir.Visibility = Visibility.Visible;
            }

            if (_regs.Length != 0)
            {
                TvReg.ItemsSource = _regs;
                TvReg.Visibility = Visibility.Visible;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_sizeChanged)
                _sizeChanged = CenterOwner();
        }
    }
}