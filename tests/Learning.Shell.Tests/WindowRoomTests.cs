using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Learning.Core.Learners;
using Learning.Shell.ViewModels;

namespace Learning.Shell.Tests;

/// <summary>
/// The window's room and colours, from WebDevelopment+ (its plan 13.3-13.9) as PythonProgramming+ took them (Learning Core
/// PLUSBOOK-FEATURES, "The window's room"; this plan W.1-W.6 and F.1): PlusBook Tools first; one part, so no part rows, and
/// Collapse Menu folds the modules; the path's block folding down; help hiding to the right; the start screen in coloured
/// sections; "In development" for modules 9, 10 and 12; Help ▸ Send feedback.
/// </summary>
public sealed partial class WindowRoomTests
{
    // Every test run keeps its learners in a folder of its own, never a tester's %ProgramData%\PLCPlus.
    private static readonly string Data = Path.Combine(Path.GetTempPath(), "PLCPlus-window-tests", Guid.NewGuid().ToString("N")[..8]);

    static WindowRoomTests() => Environment.SetEnvironmentVariable("PLCPLUS_DATA", Data);

    private static (MainWindow Window, LearnerFile File) NewWindow(string learner)
    {
        var store = new LearnerStore(LearnerPlaces.DefaultLearnersFolder, LearnerPlaces.Options);
        Directory.CreateDirectory(store.Folder);
        var (file, _) = store.Create(learner + " " + Guid.NewGuid().ToString("N")[..6], null, "8642", DateTimeOffset.Now);
        var window = new MainWindow
        {
            Width = 1400, Height = 1000, WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000,
            ShowInTaskbar = false, ShowActivated = false, OfferBackupOnClose = false,
        };
        window.Show();
        return (window, file);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [Fact]
    public void The_start_screen_is_in_sections_folded_with_About_PlusBooks_and_Send_feedback()
    {
        WpfHost.Run(() =>
        {
            var (window, file) = NewWindow("Room Learner");
            try
            {
                WpfHost.Pump();
                var start = window.SignInScreen;
                Assert.True(start.IsVisible);
                var folds = new[] { start.WhoExpander, start.ToolsExpander, start.FilesExpander };
                Assert.All(folds, x => Assert.False(x.IsExpanded));
                Assert.True(start.SectionsHint.IsVisible);
                Assert.NotEqual(start.WhoSection.Background, start.ToolsSection.Background);
                Assert.NotEqual(start.ToolsSection.Background, start.FilesSection.Background);
                Assert.NotEqual(start.FilesSection.Background, start.InfoSection.Background);
                // The last section stays open: About PlusBooks and Send feedback (F.1) are always in sight.
                Assert.True(start.InfoSection.IsVisible && start.PlusBooksLink.IsEnabled);
                Assert.True(start.FeedbackButton.IsVisible);
                Assert.Equal("Send fee_dback…", start.FeedbackButton.Content);

                foreach (var x in folds) x.IsExpanded = true;
                WpfHost.Pump();
                Assert.True(start.CheckReportButton.IsVisible);
                Assert.True(start.LearnersList.IsVisible);
                // Back to the start screen with no message: folded again.
                start.Show();
                WpfHost.Pump();
                Assert.All(folds, x => Assert.False(x.IsExpanded));
                // Back after a lock or Switch learner: the banner says to choose a name or type a PIN, so Who is learning? opens
                // for that (UX review 2026-11-23); the other two stay folded.
                start.Show("Locked for the next learner.");
                WpfHost.Pump();
                Assert.True(start.WhoExpander.IsExpanded);
                Assert.False(start.ToolsExpander.IsExpanded || start.FilesExpander.IsExpanded);
                Assert.True(start.LockedBanner.IsVisible); // above the sections, so folding never hides it

                window.SignIn(file);
                WpfHost.Pump();
                Assert.Equal("PlusBook Learning", window.PathTitle.Text);
                Assert.Equal("Collapse Menu", window.FoldPartsButton.Content);
                Assert.Equal("Course Links…", window.CourseLinksButton.Content);
                Assert.StartsWith("+ PlusBook Tools", window.PathKey.Text);
                Assert.EndsWith("– in development", window.PathKey.Text);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PlusBook_Tools_come_first_and_the_one_part_book_has_no_part_rows()
    {
        WpfHost.Run(() =>
        {
            var (window, file) = NewWindow("Path Learner");
            try
            {
                window.SignIn(file);
                WpfHost.Pump();
                var vm = (ShellViewModel)window.DataContext;

                // W.2: PlusBook Tools first, holding My progress, Hand in a report, My notes and the Glossary.
                Assert.Same(vm.Common, vm.Roots[0]);
                Assert.Equal("PlusBook Tools", vm.Common.Header);
                Assert.Equal([vm.Dashboard, vm.Reports, vm.Notes, vm.Glossary], vm.Common.Children);
                Assert.All(vm.Common.Children, c => Assert.Same(vm.Common, c.Parent));

                // W.1: PLC+ is one part, so no part rows; the twelve modules follow at the top of the tree.
                Assert.DoesNotContain(vm.Roots, r => r.IsPart);
                Assert.Equal(Enumerable.Range(1, 12), vm.Roots.Skip(1).Select(r => r.Module!.Number));
                Assert.Equal(Enumerable.Range(1, 12), vm.ModuleNodes.Select(n => n.Module!.Number));

                // W.6: the modules still to come say "In development", never "Coming later".
                foreach (var n in new[] { 9, 10, 12 })
                    Assert.Equal("In development", vm.ModuleNodes.Single(m => m.Module!.Number == n).Detail);
                Assert.All(vm.AllNodes, n => Assert.DoesNotContain("oming later", n.Detail + n.SpokenName + n.Tip));
                var dashboard = vm.DashboardMarkdown();
                Assert.Contains("In development", dashboard);
                Assert.Contains("– in development.", dashboard);
                Assert.DoesNotContain("coming later", dashboard, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("in PlusBook Tools at the top of the path", dashboard);

                // A module in development: its own page says so.
                vm.SelectModule(vm.ModuleNodes.Single(m => m.Module!.Number == 9).Module!);
                WpfHost.Pump();
                Assert.Equal(Visibility.Visible, window.ModulePage.Visibility);
                Assert.StartsWith("In development", window.ModuleStatusText.Text);
                Assert.StartsWith("Module 9 is in development", window.StatusText.Text); // and says what to do next
                Assert.Equal("This screen: a module in development", window.HelpScreenTitle.Text);

                // Choosing PlusBook Tools opens it with My progress.
                vm.Common.IsExpanded = false;
                vm.Common.IsSelected = true;
                WpfHost.Pump();
                Assert.True(vm.Common.IsExpanded);
                Assert.True(vm.Dashboard.IsSelected);
                Assert.Equal(Visibility.Visible, window.DashboardPage.Visibility);

                // Collapse Menu folds PlusBook Tools and every module; going to an item opens only its module.
                window.FoldPartsButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WpfHost.Pump();
                Assert.All(vm.AllNodes, n => Assert.False(n.IsExpanded));
                var m3 = vm.ModuleNodes.Single(n => n.Module!.Number == 3);
                vm.Select(m3.Children[0].Item!);
                WpfHost.Pump();
                Assert.True(m3.IsExpanded);
                Assert.Equal(1, vm.ModuleNodes.Count(n => n.IsExpanded));
                Assert.False(vm.Common.IsExpanded);
                Assert.StartsWith("plc.m3.", vm.CurrentItem!.Id);

                // Folding the module that holds the item on screen keeps the item there.
                var onScreen = vm.CurrentItem;
                m3.IsExpanded = false;
                WpfHost.Pump();
                Assert.Same(onScreen, vm.CurrentItem);
                Assert.Equal(Visibility.Visible, window.ItemPage.Visibility);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void The_block_under_the_path_folds_down_and_help_hides_to_the_right_each_kept_for_the_learner()
    {
        WpfHost.Run(() =>
        {
            var (window, file) = NewWindow("Fold Learner");
            try
            {
                window.SignIn(file);
                WpfHost.Pump();

                // W.3: the block folds down to its title, so the tree is taller; the choice is kept.
                Assert.True(window.PathInfo.IsExpanded);
                var treeHeight = window.PathTree.ActualHeight;
                window.PathInfo.IsExpanded = false;
                WpfHost.Pump();
                Assert.True(window.PathTree.ActualHeight > treeHeight + 40, $"{window.PathTree.ActualHeight} vs {treeHeight}");
                Assert.Equal("folded", file.Data.Preferences["pathInfo"]);
                window.PathInfo.IsExpanded = true;
                WpfHost.Pump();
                Assert.Equal("open", file.Data.Preferences["pathInfo"]);

                // W.4: Hide help leaves a strip at the right edge; the page gains the room; the strip opens help again.
                Assert.True(window.HelpPanel.IsVisible);
                Assert.False(window.HelpStrip.IsVisible);
                var pageWidth = window.PageColumn.ActualWidth;
                Click(window.FoldHelpButton);
                WpfHost.Pump();
                Assert.False(window.HelpPanel.IsVisible);
                Assert.True(window.HelpStrip.IsVisible);
                Assert.True(window.PageColumn.ActualWidth > pageWidth + 200, $"{window.PageColumn.ActualWidth} vs {pageWidth}");
                Assert.Equal("folded", file.Data.Preferences["helpPanel"]);
                Click(window.HelpStripButton);
                WpfHost.Pump();
                Assert.True(window.HelpPanel.IsVisible);
                Assert.False(window.HelpStrip.IsVisible);
                Assert.Equal("open", file.Data.Preferences["helpPanel"]);

                // W.5: the path lavender, its block light green, help light blue.
                Assert.Equal(Brush("FilesBrush"), ((Grid)window.PathTree.Parent).Background);
                Assert.Equal(Brush("LearningBrush"), window.PathInfoBack.Background);
                Assert.Equal(Brush("HelpPanelBrush"), window.HelpPanel.Background);
                Assert.Equal(Brush("HelpBarBrush"), window.FoldHelpButton.Background);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void A_machine_tab_has_the_room_and_help_comes_back_from_the_strip()
    {
        WpfHost.Run(() =>
        {
            var (window, file) = NewWindow("Machine Learner");
            try
            {
                window.SignIn(file);
                WpfHost.Pump();
                var vm = (ShellViewModel)window.DataContext;
                // An exercise with a machine (Module 5, basic ladder): help steps aside for the rungs and the strip shows.
                var item = vm.Package.AllItems.First(i => i.Module.Number == 5 && i.Exercise is not null);
                vm.Select(item);
                WpfHost.Pump();
                window.ItemTabs.SelectedItem = window.ExerciseTab;
                WpfHost.Pump();
                Assert.True(window.ExerciseWorkspace.IsVisible);
                Assert.False(window.HelpPanel.IsVisible);
                Assert.True(window.HelpStrip.IsVisible);
                Assert.Contains("Help strip", window.StatusText.Text);
                // Help stepping aside by itself is not the learner's choice: nothing is kept.
                Assert.False(file.Data.Preferences.ContainsKey("helpPanel"));
                Assert.True(window.ExerciseWorkspace.ActualWidth > 700, $"{window.ExerciseWorkspace.ActualWidth}");
                Click(window.HelpStripButton);
                WpfHost.Pump();
                Assert.True(window.HelpPanel.IsVisible);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Send_feedback_names_PLC_plus_and_this_version()
    {
        Assert.Matches(@"^https://softwareplus\.ai/feedback/\?program=plcplus&version=\d+\.\d+\.\d+$", WebLinks.Feedback);
        Assert.Equal("https://softwareplus.ai/plusbooks/", WebLinks.PlusBooks);
        // Help ▸ Send feedback… sits beside Check for a new version, with the portfolio's words.
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Learning.Shell", "MainWindow.xaml"));
        Assert.Matches("Header=\"Send _feedback…\" Click=\"SendFeedback_Click\"\\s+ToolTip=\"Tell us about a bug, a suggestion or anything you noticed — it goes to Dr. Ron Eaglin.\"", xaml);
        Assert.True(xaml.IndexOf("Check for a _new version", StringComparison.Ordinal) < xaml.IndexOf("Send _feedback", StringComparison.Ordinal));
    }

    [Fact]
    public void The_content_says_Hand_in_a_report_is_in_PlusBook_Tools()
    {
        var modules = Path.Combine(RepoRoot(), "content", "plcplus-foundations", "modules");
        var found = 0;
        foreach (var path in Directory.GetFiles(modules, "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("second row of the path", text);
            foreach (Match m in HandIn().Matches(text))
            {
                found++;
                Assert.Contains("PlusBook Tools", m.Value);
            }
        }
        Assert.True(found >= 9, $"{found} directions to Hand in a report");
    }

    [GeneratedRegex(@"\*\*Hand in a report\*\* \([^)]*\)")]
    private static partial Regex HandIn();

    private static System.Windows.Media.Brush Brush(string key) => (System.Windows.Media.Brush)Application.Current.FindResource(key);

    private static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "PLCPlus.slnx"))) return d.FullName;
        throw new DirectoryNotFoundException("PLCPlus.slnx not found above the test folder");
    }
}
