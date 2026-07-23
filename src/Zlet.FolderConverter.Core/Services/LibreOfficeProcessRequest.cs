using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed record LibreOfficeProcessRequest(
    string ExecutablePath,
    string SourcePath,
    string OutputDirectory,
    string UserProfileDirectory,
    ConversionTarget Target);
