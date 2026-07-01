using MyCms.Scaffolding.Models;

namespace MyCms.Scaffolding;

/// <summary>
/// Legge lo schema di un database SQL Server tramite query dirette sulle viste
/// di sistema (sys.tables, sys.columns, sys.foreign_keys). Nessuna dipendenza
/// da API interne di EF Core: solo T-SQL standard, stabile tra versioni.
/// </summary>
public interface ISqlServerSchemaReader
{
    /// <summary>
    /// Elenca le tabelle utente disponibili per lo scaffolding (usate nel primo
    /// step del wizard). Esclude tabelle di sistema, lo schema "cms" del CMS
    /// stesso e la tabella di storico delle migration EF.
    /// </summary>
    Task<IReadOnlyList<DatabaseTableInfo>> GetTablesAsync(CancellationToken ct = default);

    /// <summary>Legge colonne e foreign key in uscita di una singola tabella.</summary>
    Task<DatabaseTableDetails> GetTableDetailsAsync(string schemaName, string tableName, CancellationToken ct = default);
}
