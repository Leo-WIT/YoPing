using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using vmPing.Classes;

namespace vmPing.UI
{
    public partial class HelpWindow : Window
    {
        public static HelpWindow _OpenWindow = null;

        public HelpWindow()
        {
            InitializeComponent();
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;

            Version.Inlines.Clear();
            Version.Inlines.Add(new Run("版本：1.0"));

            Copyright.Inlines.Clear();
            Copyright.Inlines.Add(new Run($"Copyright © {DateTime.Now.Year} Leo-WIT。基于原项目："));
            var originalProject = new Hyperlink(new Run("https://github.com/R-Smith/vmPing"))
            {
                NavigateUri = new Uri("https://github.com/R-Smith/vmPing")
            };
            originalProject.RequestNavigate += Hyperlink_RequestNavigate;
            Copyright.Inlines.Add(originalProject);

            // 初始焦点放到文档区域，便于用户直接用键盘滚动帮助内容。
            MainDocument.Focus();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            }
            catch
            {
                // 忽略系统无法打开链接的情况。
            }
            finally
            {
                e.Handled = true;
            }
        }

        private void Intro_Selected(object sender, RoutedEventArgs e)
        {
            Intro?.BringIntoView();
        }

        private void BasicUsage_Selected(object sender, RoutedEventArgs e)
        {
            BasicUsage?.BringIntoView();
        }

        private void ExtraFeatures_Selected(object sender, RoutedEventArgs e)
        {
            ExtraFeatures?.BringIntoView();
        }

        private void Options_Selected(object sender, RoutedEventArgs e)
        {
            Options?.BringIntoView();
        }

        private void CommandLineUsage_Selected(object sender, RoutedEventArgs e)
        {
            CommandLineUsage?.BringIntoView();
        }

        private void Window_Loaded(object sender, EventArgs e)
        {
            _OpenWindow = this;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _OpenWindow = null;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Constants.HelpKeyBinding)
            {
                e.Handled = true;
                Close();
            }
        }
    }
}
