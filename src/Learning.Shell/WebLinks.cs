using System.Diagnostics;
using System.Windows;

namespace Learning.Shell;

/// <summary>PLC+'s pages on softwareplus.ai, and opening one in the web browser (or saying where it is if that fails).</summary>
public static class WebLinks
{
    public const string Help = "https://softwareplus.ai/plcplus/help/";
    /// <summary>The instructors' testing guide and how to send feedback (plan 12.8).</summary>
    public const string Testing = "https://softwareplus.ai/plcplus/testing/";
    public const string Privacy = "https://softwareplus.ai/plcplus/privacy/";

    public static void Open(Window? owner, string url, string what)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            var text = $"Windows could not open your web browser. {what} is at:\n\n{url}";
            if (owner is null) MessageBox.Show(text, "PLC+ — " + what, MessageBoxButton.OK, MessageBoxImage.Information);
            else MessageBox.Show(owner, text, "PLC+ — " + what, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
