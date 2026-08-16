using Microsoft.AspNetCore.Http;

namespace DAMIHeadlessCMS.Admin.Data;

/// <summary>
/// Astrae la persistenza fisica dei file caricati dai campi EditorType.File
/// che referenziano il file tramite un percorso/nome (colonne di tipo stringa).
/// Le colonne varbinary/binary/image, invece, memorizzano i byte direttamente
/// nel database e non passano da qui: sono gestite dentro GenericEntityRepository.
/// </summary>
public interface IFileStorageProvider
{
    /// <summary>Salva il file e ritorna il percorso relativo da persistere nella colonna.</summary>
    Task<string> SaveAsync(IFormFile file, string subFolder, CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="SaveAsync(IFormFile, string, CancellationToken)"/> ma per contenuto
    /// già disponibile come stream (es. scaricato da un URL esterno) invece che da un
    /// upload multipart — pensato per migrazioni one-off di file storici da fonti esterne
    /// verso lo storage locale (vedi TestHost/PublicSite/LegacyMediaMigrationService).
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken ct = default);

    /// <summary>Elimina un file precedentemente salvato, se esiste. Non solleva eccezioni se assente.</summary>
    Task DeleteAsync(string? relativePath, CancellationToken ct = default);
}