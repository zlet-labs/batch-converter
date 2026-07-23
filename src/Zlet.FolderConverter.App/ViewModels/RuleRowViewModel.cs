using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class RuleRowViewModel : INotifyPropertyChanged
{
    private readonly Action<SourceFormat, ConversionTarget> _selectionChanged;
    private ConversionTargetOption _selectedTarget;

    public RuleRowViewModel(
        FormatCapability capability,
        int count,
        ConversionTarget selectedTarget,
        Action<SourceFormat, ConversionTarget> selectionChanged)
    {
        SourceFormat = capability.SourceFormat;
        FormatLabel = capability.DisplayName;
        Count = count;
        Targets = capability.AllowedTargets
            .Select(target => new ConversionTargetOption(target, target.ToDisplayName()))
            .ToArray();
        _selectedTarget = Targets.Single(option => option.Target == selectedTarget);
        _selectionChanged = selectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SourceFormat SourceFormat { get; }
    public string FormatLabel { get; }
    public int Count { get; }
    public IReadOnlyList<ConversionTargetOption> Targets { get; }

    public ConversionTargetOption SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (value is null || value == _selectedTarget)
            {
                return;
            }

            _selectedTarget = value;
            OnPropertyChanged();
            _selectionChanged(SourceFormat, value.Target);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
