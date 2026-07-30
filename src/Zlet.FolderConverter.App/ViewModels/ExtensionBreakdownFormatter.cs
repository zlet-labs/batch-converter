using System.IO;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public static class ExtensionBreakdownFormatter
{
    private const string NoExtensionLabel = "Без расширения";

    public static string Format(IEnumerable<ScannedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return string.Join(
            " · ",
            files.Select(file => GetExtensionLabel(file.RelativePath))
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

    private static string GetExtensionLabel(string relativePath)
    {
        try
        {
            var extension = Path.GetExtension(relativePath);
            return string.IsNullOrWhiteSpace(extension)
                ? NoExtensionLabel
                : extension.TrimStart('.').ToUpperInvariant();
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return NoExtensionLabel;
        }
    }
}
