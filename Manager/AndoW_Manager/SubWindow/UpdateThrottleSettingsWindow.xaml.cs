using System;
using System.Globalization;
using System.Windows;
using AndoW.Shared;

namespace AndoW_Manager
{
    public partial class UpdateThrottleSettingsWindow : Window
    {
        private readonly UpdateThrottleSettingsManager settingsManager;
        private UpdateThrottleSettings settings;

        public UpdateThrottleSettingsWindow()
        {
            InitializeComponent();
            settingsManager = new UpdateThrottleSettingsManager();
            LoadSettings();
            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += CancelButton_Click;
        }

        private void LoadSettings()
        {
            settings = settingsManager.LoadSettings();
            if (settings == null)
            {
                settings = new UpdateThrottleSettings();
            }

            MaxConcurrentTextBox.Text = settings.MaxConcurrentDownloads.ToString(CultureInfo.InvariantCulture);
            RetryIntervalTextBox.Text = settings.RetryIntervalSeconds.ToString(CultureInfo.InvariantCulture);
            LeaseTtlTextBox.Text = settings.LeaseTtlSeconds.ToString(CultureInfo.InvariantCulture);
            LeaseRenewTextBox.Text = settings.LeaseRenewIntervalSeconds.ToString(CultureInfo.InvariantCulture);
            SettingsRefreshTextBox.Text = settings.SettingsRefreshSeconds.ToString(CultureInfo.InvariantCulture);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadInt(MaxConcurrentTextBox.Text, out int maxConcurrent)
                || !TryReadInt(RetryIntervalTextBox.Text, out int retryInterval)
                || !TryReadInt(LeaseTtlTextBox.Text, out int leaseTtl)
                || !TryReadInt(LeaseRenewTextBox.Text, out int leaseRenew)
                || !TryReadInt(SettingsRefreshTextBox.Text, out int refresh))
            {
                MessageBox.Show("Invalid numeric input.", "Update Throttle Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            settings.MaxConcurrentDownloads = maxConcurrent;
            settings.RetryIntervalSeconds = retryInterval;
            settings.LeaseTtlSeconds = leaseTtl;
            settings.LeaseRenewIntervalSeconds = leaseRenew;
            settings.SettingsRefreshSeconds = refresh;
            settingsManager.SaveSettings(settings);

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnWin_close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static bool TryReadInt(string raw, out int value)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(true);
        }
    }
}
