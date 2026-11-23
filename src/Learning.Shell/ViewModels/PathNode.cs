using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Learning.Core.Content;

namespace Learning.Shell.ViewModels;

/// <summary>A row of PlusBook Learning's tree: PlusBook Tools and its rows, a part, a module, or an item.</summary>
public sealed partial class PathNode : ObservableObject
{
    /// <summary>The "My progress" row, first in PlusBook Tools (Ron, 2026-11-05; plan W.2).</summary>
    public bool IsDashboard { get; init; }
    /// <summary>The "Hand in a report" row, under My progress (plan 8.2).</summary>
    public bool IsReports { get; init; }
    /// <summary>The "My notes" row, under Hand in a report (Learning Core plan 3.2).</summary>
    public bool IsNotes { get; init; }
    /// <summary>The "Glossary" row, under My notes (Learning Core plan 3.13).</summary>
    public bool IsGlossary { get; init; }
    /// <summary>"PlusBook Tools" (plan W.2): the folding row that holds the rows above, so the book's materials come first.</summary>
    public bool IsCommon { get; init; }
    /// <summary>
    /// A part of the book (Learning Core's <c>part:</c>; plan W.1). PLC+ has one part today, so it has no part rows; if its
    /// content ever names parts, each holds its modules and folds on its own.
    /// </summary>
    public bool IsPart { get; init; }
    public LearningModule? PartFirst { get; init; }
    public LearningModule? Module { get; init; }
    public LearningItem? Item { get; init; }
    /// <summary>The row this one is under: an item's module, a module's part, a tool's PlusBook Tools (null at the top).</summary>
    public PathNode? Parent { get; init; }
    public ObservableCollection<PathNode> Children { get; } = [];

    /// <summary>How far in the row sits (0 at the top), so its words wrap inside the path's column.</summary>
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;
    /// <summary>The width the row's words wrap at: each level in the tree is indented 19 px.</summary>
    public double TextWidth => 230 - 19 * Depth;

    [ObservableProperty] private string _header = "";
    [ObservableProperty] private string _mark = "";
    [ObservableProperty] private Brush _markBrush = Brushes.Gray;
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _spokenName = "";
    [ObservableProperty] private string _tip = "";
    [ObservableProperty] private FontWeight _weight = FontWeights.Normal;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;
}
