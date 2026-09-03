using System.IO;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public static class ExtensionBreakdownFormatter
{
    public static string Format(
        IEnumerable<ScannedFile> files,
        Localization.LocalizationService? localization = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        localization ??= Localization.LocalizationService.Current;

        return string.Join(
            " · ",
            files.Select(file => GetExtensionLabel(file.RelativePath, localization))
                .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Label = group.Key,
                    Count = group.Count()
                })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Label}: {item.Count}"));
    }

    private static string GetExtensionLabel(
        string relativePath,
        Localization.LocalizationService localization)
    {
        try
        {
            var extension = Path.GetExtension(relativePath);
            return string.IsNullOrWhiteSpace(extension)
                ? localization.Get("NoExtension")
                : extension.TrimStart('.').ToUpperInvariant();
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return localization.Get("NoExtension");
        }
    }
}
