using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.App.Localization;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class RuleRowViewModel : INotifyPropertyChanged
{
    private readonly Action<SourceFormat, ConversionTarget> _selectionChanged;
    private ConversionTargetOption _selectedTarget;

    public RuleRowViewModel(
        FormatCapability capability,
        int count,
        ConversionTarget selectedTarget,
        Action<SourceFormat, ConversionTarget> selectionChanged,
        string extensionBreakdown = "")
    {
        SourceFormat = capability.SourceFormat;
        FormatLabel = capability.DisplayName;
        Count = count;
        ExtensionBreakdown = extensionBreakdown;
        Targets = capability.AllowedTargets
            .Select(target => new ConversionTargetOption(target, target switch
            {
                ConversionTarget.Skip => LocalizationService.Current.Get("TargetSkip"),
                ConversionTarget.Copy => LocalizationService.Current.Get("TargetCopy"),
                _ => target.ToDisplayName()
            }))
            .ToArray();
        _selectedTarget = Targets.Single(option => option.Target == selectedTarget);
        _selectionChanged = selectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SourceFormat SourceFormat { get; }
    public string FormatLabel { get; }
    public int Count { get; }
    public string ExtensionBreakdown { get; }
    public bool HasExtensionBreakdown => !string.IsNullOrWhiteSpace(ExtensionBreakdown);
    public IReadOnlyList<ConversionTargetOption> Targets { get; private set; }

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

    public void RefreshLocalization()
    {
        var selected = _selectedTarget.Target;
        Targets = Targets.Select(option => new ConversionTargetOption(
            option.Target,
            option.Target switch
            {
                ConversionTarget.Skip => LocalizationService.Current.Get("TargetSkip"),
                ConversionTarget.Copy => LocalizationService.Current.Get("TargetCopy"),
                _ => option.Target.ToDisplayName()
            })).ToArray();
        _selectedTarget = Targets.Single(option => option.Target == selected);
        OnPropertyChanged(nameof(Targets));
        OnPropertyChanged(nameof(SelectedTarget));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
