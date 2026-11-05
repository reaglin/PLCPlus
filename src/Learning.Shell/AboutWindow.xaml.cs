using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace Learning.Shell;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {v?.ToString(3)}";
        // Shown only once the Store ID is in, and only if the address is a real one (a typo must not stop About opening).
        if (Uri.TryCreate(StoreUpdates.StorePage, UriKind.Absolute, out var store))
        {
            StoreLink.NavigateUri = store;
            StoreLinkText.Text = StoreUpdates.StorePage.Replace("https://", "", StringComparison.Ordinal);
            StoreLine.Visibility = Visibility.Visible;
        }
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        WebLinks.Open(this, e.Uri.AbsoluteUri, "The page");
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
