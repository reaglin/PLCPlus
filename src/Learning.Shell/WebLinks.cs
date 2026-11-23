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
    /// <summary>
    /// The feedback form (plan F.1; portfolio rule 2026-11-20): it names PLC+ and this version, so the inbox knows where a note
    /// came from. No form inside the program and no network code: the web page does the rest.
    /// </summary>
    public static string Feedback =>
        "https://softwareplus.ai/feedback/?program=plcplus&version="
        + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "");
    /// <summary>What a PlusBook is, and every PlusBook (the start screen's About PlusBooks; Ron, 2026-11-23, plan W.5).</summary>
    public const string PlusBooks = "https://softwareplus.ai/plusbooks/";

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
