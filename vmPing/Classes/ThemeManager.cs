using System;
using System.Linq;
using System.Windows;

namespace vmPing.Classes
{
    public static class ThemeManager
    {
        private const string LightThemeSource = "ResourceDictionaries/BootstrapTheme.xaml";
        private const string DarkThemeSource = "ResourceDictionaries/BootstrapDarkTheme.xaml";

        public static void ApplyCurrentTheme()
        {
            ApplyTheme(ApplicationOptions.Theme);
        }

        public static void ApplyTheme(ApplicationOptions.ThemeMode theme)
        {
            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            var dictionaries = app.Resources.MergedDictionaries;
            var existingTheme = dictionaries.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.EndsWith(LightThemeSource, StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.EndsWith(DarkThemeSource, StringComparison.OrdinalIgnoreCase)));

            if (existingTheme != null)
            {
                dictionaries.Remove(existingTheme);
            }

            var source = theme == ApplicationOptions.ThemeMode.Dark ? DarkThemeSource : LightThemeSource;
            dictionaries.Insert(0, new ResourceDictionary
            {
                Source = new Uri(source, UriKind.Relative)
            });
        }
    }
}
