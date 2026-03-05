using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using QBBTI.App.Services;
using QBBTI.App.ViewModels;

namespace QBBTI.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DialogService());
        Closing += (_, _) => (DataContext as MainViewModel)?.Disconnect();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "Unknown";

        // Strip the +build hash suffix if present
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        var aboutWindow = new Window
        {
            Title = "About QBBTI",
            Width = 380,
            Height = 280,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ShowInTaskbar = false,
            Icon = Icon
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "QuickBooks Bank Transaction Importer",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"Version {version}",
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 16)
        });

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Kamron Batman",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var link = new Hyperlink(new Run("github.com/kamronbatman/QBBTI"))
        {
            NavigateUri = new Uri("https://github.com/kamronbatman/QBBTI")
        };
        link.RequestNavigate += (_, args) =>
        {
            Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
            args.Handled = true;
        };
        panel.Children.Add(new System.Windows.Controls.TextBlock(link)
        {
            Margin = new Thickness(0, 0, 0, 16)
        });

        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Licensed under the GNU General Public License v3.0",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.Gray,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 20)
        });

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)FindResource("PrimaryButton")
        };
        okButton.Click += (_, _) => aboutWindow.Close();
        panel.Children.Add(okButton);

        aboutWindow.Content = panel;
        aboutWindow.ShowDialog();
    }
}
