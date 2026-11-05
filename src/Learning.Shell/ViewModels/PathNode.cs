using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Learning.Core.Content;

namespace Learning.Shell.ViewModels;

/// <summary>A row of the learning path's tree: My progress, Hand in a report, a module, or an item.</summary>
public sealed partial class PathNode : ObservableObject
{
    /// <summary>The "My progress" row at the top of the tree (Ron, 2026-11-05).</summary>
    public bool IsDashboard { get; init; }
    /// <summary>The "Hand in a report" row, under My progress (plan 8.2).</summary>
    public bool IsReports { get; init; }
    public LearningModule? Module { get; init; }
    public LearningItem? Item { get; init; }
    public PathNode? Parent { get; init; }
    public ObservableCollection<PathNode> Children { get; } = [];

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
