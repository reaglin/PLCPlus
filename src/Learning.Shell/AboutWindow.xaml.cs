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
        if (StoreUpdates.StorePage.Length > 0)
        {
            StoreLink.NavigateUri = new Uri(StoreUpdates.StorePage);
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
