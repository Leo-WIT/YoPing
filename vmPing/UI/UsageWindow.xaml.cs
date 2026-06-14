using System;
using System.Windows;

namespace vmPing.UI
{
    public partial class UsageWindow : Window
    {
        public UsageWindow()
        {
            InitializeComponent();

            Version version = typeof(MainWindow).Assembly.GetName().Version;
            AppVersion.Text = version.Build == 0 ? $"{version.Major}.{version.Minor}" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
