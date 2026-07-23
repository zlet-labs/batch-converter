using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class FormatCardViewModel(string name, bool isSupported = false) : INotifyPropertyChanged
{
    private int _count;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; } = name;

    public int Count
    {
        get => _count;
        set
        {
            if (_count == value)
            {
                return;
            }

            _count = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string CountText => FormatFileCount(Count);

    public string StatusText => Count == 0
        ? (isSupported ? "JSON → TXT/MD" : "Конвертация недоступна")
        : (isSupported ? "Конвертация доступна" : "Конвертация недоступна");

    public static string FormatFileCount(int count)
    {
        var suffix = count % 10 == 1 && count % 100 != 11
            ? "файл"
            : count % 10 is >= 2 and <= 4 && (count % 100 is < 12 or > 14)
                ? "файла"
                : "файлов";

        return $"{count} {suffix}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
