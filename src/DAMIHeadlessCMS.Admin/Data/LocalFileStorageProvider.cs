using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DAMIHeadlessCMS.Admin.Data;

/// <summary>
/// Implementazione di default: salva i file in wwwroot/uploads dell'app HOST
/// (non della Razor Class Library), così da essere serviti direttamente da
/// UseStaticFiles() senza configurazione aggiuntiva. Nome file randomizzato
/// per evitare collisioni e problemi di path traversal da nomi utente.
/// </summary>
public class LocalFileStorageProvider : IFileStorageProvider
{
    private const string RootFolder = "uploads";
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageProvider(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveAsync(IFormFile file, string subFolder, CancellationToken ct = default)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeSubFolder = string.Join('_', subFolder.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        var targetDirectory = Path.Combine(_env.WebRootPath, RootFolder, safeSubFolder);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"{RootFolder}/{safeSubFolder}/{fileName}".Replace('\\', '/');
    }

    public Task DeleteAsync(string? relativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.Combine(_env.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}