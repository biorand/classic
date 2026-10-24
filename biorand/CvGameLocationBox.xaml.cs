using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for CvGameLocationBox.xaml
    /// </summary>
    public partial class CvGameLocationBox : UserControl
    {
        public event EventHandler Changed;
        public event EventHandler<PathValidateEventArgs> ValidateIso;
        public event EventHandler<PathValidateEventArgs> ValidatePc;

        /// <summary>
        /// Flag to know if the settings are completely loaded. Used to avoid triggering a save when we are loading the configs.
        /// </summary>
        public bool IsSettingsLoaded { get; set; }

        private bool _isPc;
        private bool _suspendEvents;

        public CvGameLocationBox()
        {
            InitializeComponent();
            _suspendEvents = true;
            try
            {
                cbVersion.SelectedIndex = 0;
            }
            finally
            {
                _suspendEvents = false;
            }
            UpdateVersionUi();
        }

        public bool IsChecked
        {
            get => groupBox.IsChecked == true;
            set => groupBox.IsChecked = value;
        }

        public bool IsPc
        {
            get => _isPc;
            set
            {
                _isPc = value;
                _suspendEvents = true;
                try
                {
                    cbVersion.SelectedIndex = value ? 1 : 0;
                }
                finally
                {
                    _suspendEvents = false;
                }
                UpdateVersionUi();
            }
        }

        public string IsoLocation
        {
            get => txtIsoLocation.Text;
            set => txtIsoLocation.Text = value;
        }

        public string PcLocation
        {
            get => txtPcLocation.Text;
            set => txtPcLocation.Text = value;
        }

        public string SelectedExecutable => cbExecutables.SelectedItem?.ToString();

        public bool DoorSkip
        {
            get => chkDoorSkip.IsChecked == true;
            set => chkDoorSkip.IsChecked = value;
        }

        /// <summary>
        /// Load the executables (exe) from the selected PC folder and set the default selection.
        /// </summary>
        /// <param name="directory">The folder containing the PC version of the game</param>
        /// <param name="selection">The exe name including the extension</param>
        public void SetExecutables(string directory, string selection)
        {
            RefreshExecutables(directory, selection);
        }

        private void RefreshExecutables(string directory, string selection)
        {
            var previousSelection = SelectedExecutable;
            _suspendEvents = true;
            try
            {
                cbExecutables.Items.Clear();
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    var executables = Directory.GetFiles(directory, "*.exe")
                        .Select(Path.GetFileName)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (var executable in executables)
                    {
                        cbExecutables.Items.Add(executable);
                    }

                    var selectedExecutable = selection ?? previousSelection;
                    var match = executables.FirstOrDefault(x => string.Equals(x, selectedExecutable, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                    {
                        match = executables.FirstOrDefault(x => string.Equals(x, "recvx.exe", StringComparison.OrdinalIgnoreCase));
                    }
                    cbExecutables.SelectedItem = match;
                }
            }
            finally
            {
                _suspendEvents = false;
            }
        }

        private void UpdateVersionUi()
        {
            var pcVisibility = _isPc ? Visibility.Visible : Visibility.Collapsed;

            lblPcFolder.Visibility = pcVisibility;
            txtPcLocation.Visibility = pcVisibility;
            cbExecutables.Visibility = pcVisibility;
            btnBrowsePc.Visibility = pcVisibility;
            txtPcValidationMessage.Visibility = pcVisibility;

            lblIso.Text = _isPc ? "Game data ISO" : "PS2 ISO";
            txtIsoLocation.Visibility = Visibility.Visible;
            btnBrowseIso.Visibility = Visibility.Visible;
            txtIsoValidationMessage.Visibility = Visibility.Visible;
            lblIso.Visibility = Visibility.Visible;
            chkDoorSkip.Visibility = _isPc ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ValidateIsoPath(string path)
        {
            var eventArgs = new PathValidateEventArgs();
            eventArgs.Path = path;
            try
            {
                ValidateIso?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                eventArgs.Message = ex.Message;
                eventArgs.IsValid = false;
            }

            SetValidationMessage(txtIsoValidationMessage, eventArgs);
        }

        private void ValidatePcPath(string path)
        {
            var eventArgs = new PathValidateEventArgs();
            eventArgs.Path = path;
            try
            {
                ValidatePc?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                eventArgs.Message = ex.Message;
                eventArgs.IsValid = false;
            }

            SetValidationMessage(txtPcValidationMessage, eventArgs);
        }

        private static void SetValidationMessage(TextBlock textBlock, PathValidateEventArgs eventArgs)
        {
            textBlock.Text = eventArgs.Message ?? "";
            textBlock.Foreground = eventArgs.IsValid ?
                Brushes.Green :
                Brushes.Red;
        }

        private void ValidateActivePaths()
        {
            if (_isPc)
            {
                ValidatePcPath(PcLocation);
                ValidateIsoPath(IsoLocation);
            }
            else
            {
                ValidateIsoPath(IsoLocation);
            }
        }

        private void InvokeChanges()
        {
            if (IsSettingsLoaded)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void cbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suspendEvents)
            {
                return;
            }

            _isPc = cbVersion.SelectedIndex == 1;
            UpdateVersionUi();
            if (_isPc)
            {
                SetExecutables(PcLocation, null);
            }
            ValidateActivePaths();
            InvokeChanges();
        }

        private void btnBrowseIso_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select Resident Evil CV:X ISO";
            var directory = string.IsNullOrEmpty(IsoLocation) ? null : Path.GetDirectoryName(IsoLocation);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
            dialog.Filter = "ISO image (*.iso)|*.iso";
            var window = Window.GetWindow(this);
            if (dialog.ShowDialog(window) == true)
            {
                txtIsoLocation.Text = dialog.FileName;
                ValidateIsoPath(dialog.FileName);
                InvokeChanges();
            }
        }

        private void btnBrowsePc_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select the PC version of Resident Evil CV:X";
            if (Directory.Exists(PcLocation))
            {
                dialog.InitialDirectory = PcLocation;
            }
            dialog.Filter = "recvx.exe|recvx.exe|Executable Files (*.exe)|*.exe";
            var window = Window.GetWindow(this);
            if (dialog.ShowDialog(window) == true)
            {
                var directory = Path.GetDirectoryName(dialog.FileName);
                txtPcLocation.Text = directory;
                SetExecutables(directory, Path.GetFileName(dialog.FileName));
                ValidatePcPath(directory);
                InvokeChanges();
            }
        }

        private void txtIsoLocation_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateIsoPath(IsoLocation);
            InvokeChanges();
        }

        private void txtPcLocation_LostFocus(object sender, RoutedEventArgs e)
        {
            SetExecutables(PcLocation, null);
            ValidatePcPath(PcLocation);
            InvokeChanges();
        }

        private void cbExecutables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suspendEvents)
            {
                InvokeChanges();
            }
        }

        private void groupBox_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            InvokeChanges();
        }

        private void chkDoorSkip_Changed(object sender, RoutedEventArgs e)
        {
            InvokeChanges();
        }
    }
}
