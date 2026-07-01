using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyCms.Data;
using MyCms.Scaffolding;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' non trovata. Copia appsettings.example.json in appsettings.json e valorizzala.");

var optionsBuilder = new DbContextOptionsBuilder<CmsDbContext>();
optionsBuilder.UseSqlServer(connectionString);

await using var db = new CmsDbContext(optionsBuilder.Options);
var reader = new SqlServerSchemaReader(connectionString);
var scaffoldingService = new ScaffoldingService(reader, db);

Console.WriteLine("== Tabelle disponibili per lo scaffolding ==");
var tables = await scaffoldingService.GetAvailableTablesAsync();

if (tables.Count == 0)
{
    Console.WriteLine("Nessuna tabella utente trovata nel database (a parte lo schema 'cms').");
    return;
}

for (var i = 0; i < tables.Count; i++)
{
    Console.WriteLine($"  [{i + 1}] {tables[i].QualifiedName}");
}

Console.WriteLine();
Console.WriteLine("Inserisci i numeri delle tabelle da scaffoldare, separati da virgola (es. 1,3,4), oppure INVIO per uscire:");
var input = Console.ReadLine();

if (string.IsNullOrWhiteSpace(input))
{
    return;
}

var selectedIndexes = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(s => int.Parse(s) - 1)
    .ToList();

var selectedTables = selectedIndexes.Select(i => tables[i]).ToList();

Console.WriteLine();
Console.WriteLine("Scaffolding in corso...");
var entities = await scaffoldingService.ScaffoldTablesAsync(selectedTables);

Console.WriteLine();
Console.WriteLine("== Risultato ==");
foreach (var entity in entities)
{
    Console.WriteLine($"- {entity.DisplayName} ({entity.QualifiedTableName}), PK: {entity.PrimaryKeyColumn}");
    foreach (var field in entity.Fields.OrderBy(f => f.SortOrder))
    {
        var fkInfo = field.IsForeignKey ? $" -> FK verso EntityDefinitionId={field.ForeignKeyTargetEntityId}" : "";
        Console.WriteLine($"    {field.ColumnName,-25} {field.SqlDataType,-15} EditorType={field.EditorType,-10} Required={field.IsRequired}{fkInfo}");
    }
}
