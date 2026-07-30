namespace Zlet.FolderConverter.Core.Services;

public interface IOutputResultValidator
{
    Models.OutputValidationResult Validate(string targetPath, Models.ConversionTarget target);
}
