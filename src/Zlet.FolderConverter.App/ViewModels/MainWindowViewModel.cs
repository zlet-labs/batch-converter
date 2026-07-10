using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFolderScanner _folderScanner;
    private readonly IConversionPlanner _conversionPlanner;
    private string _selectedFolder = string.Empty;
    private bool _includeSubfolders = true;
    private bool _isScanning;
    private int _docCount;
    private int _xlsCount;
    private int _pptCount;
    private string _stateMessage = "Choose a folder to start scanning.";
    private string _emptyStateMessage = "No folder scanned yet.";

    public MainWindowViewModel(
        IFolderScanner folderScanner,
        IConversionPlanner conversionPlanner)
    {
        _folderScanner = folderScanner;
        _conversionPlanner = conversionPlanner;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OperationRowViewModel> Operations { get; } = [];

    public ObservableCollection<string> ErrorMessages { get; } = [];

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set => SetProperty(ref _includeSubfolders, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    public bool CanScan => !IsScanning && Directory.Exists(SelectedFolder);

    public int DocCount
    {
        get => _docCount;
        private set => SetProperty(ref _docCount, value);
    }

    public int XlsCount
    {
        get => _xlsCount;
        private set => SetProperty(ref _xlsCount, value);
    }

    public int PptCount
    {
        get => _pptCount;
        private set => SetProperty(ref _pptCount, value);
    }

    public string StateMessage
    {
        get => _stateMessage;
        set => SetProperty(ref _stateMessage, value);
    }

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set => SetProperty(ref _emptyStateMessage, value);
    }

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(SelectedFolder))
        {
            StateMessage = "Selected folder does not exist.";
            return;
        }

        IsScanning = true;
        StateMessage = "Scanning...";
        EmptyStateMessage = "Scanning selected folder...";
        Operations.Clear();
        ErrorMessages.Clear();

        try
        {
            var scanResult = await _folderScanner.ScanAsync(
                SelectedFolder,
                IncludeSubfolders,
                cancellationToken);

            DocCount = scanResult.DocCount;
            XlsCount = scanResult.XlsCount;
            PptCount = scanResult.PptCount;

            foreach (var error in scanResult.Errors)
            {
                AddError($"{error.Path}: {error.Message}");
            }

            var plan = _conversionPlanner.CreatePlan(scanResult, SelectedFolder);
            foreach (var operation in plan)
            {
                Operations.Add(new OperationRowViewModel(operation));

                if (operation.Status is OperationStatus.Conflict or OperationStatus.Failed)
                {
                    AddError($"{operation.RelativePath}: {operation.Message}");
                }
            }

            EmptyStateMessage = Operations.Count == 0
                ? "No DOC, XLS, or PPT files found."
                : string.Empty;
            StateMessage = $"Scan complete. Found {Operations.Count} planned operation(s).";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void AddError(string message)
    {
        ErrorMessages.Add(message);
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
