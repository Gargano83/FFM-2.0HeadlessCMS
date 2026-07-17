using DAMIHeadlessCMS.Admin.Extensions;
using DAMIHeadlessCMS.Admin.Ffm;
using DAMIHeadlessCMS.Data.Extensions;
using DAMIHeadlessCMS.Data.Identity;
using DAMIHeadlessCMS.Scaffolding.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' non configurata. Crea 'appsettings.Development.json' (locale, mai committato — vedi .gitignore) valorizzando ConnectionStrings:Default.");

// Stessa connection string per CmsDbContext (metadati in schema cms.*) e per
// il repository generico (tabelle applicative in dbo/FFM/TEST/ecc.): vivono
// nello stesso database fisico.
builder.Services.AddDAMIHeadlessCMSData(connectionString);
builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
builder.Services.AddDAMIHeadlessCMSScaffolding(connectionString);
builder.Services.AddDAMIHeadlessCMSIdentity();

// Modulo opzionale: pagine dedicate per tabelle FFM specifiche (Giocatori, e in
// futuro SquadreRelGiocatori) tramite componenti Angular/Syncfusion, riservate
// a CmsAdmin. Non fa parte del core del CMS: va abilitato solo se l'host
// ospita effettivamente lo schema FFM.
builder.Services.AddDAMIHeadlessCMSFfm(connectionString);

builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

// Lettura dei contenuti (WN_Contenuti, FFM.Squadre, ecc.) per le pagine
// pubbliche simulate da questo host: codice del TestHost, non della libreria.
builder.Services.AddScoped<DAMIHeadlessCMS.TestHost.PublicSite.LegacyContentReader>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Sito pubblico (simulazione dell'app host definitiva): '/' e le rotte
// convenzionali standard vanno al HomeController del TestHost.
app.MapDefaultControllerRoute();

// Routing generico per le CmsPage native (contenuto NON proveniente dal
// legacy, es. pagine nuove create da backoffice): un solo segmento di path,
// risolto da PagesController.Show in base allo Slug. Essendo una rotta
// convenzionale "aperta", viene provata solo se nessun'altra rotta più
// specifica (attribute routing di /dami/*, rotte convenzionali verso
// controller esistenti) ha già trovato una corrispondenza.
app.MapControllerRoute(
    name: "cms-page",
    pattern: "{slug}",
    defaults: new { controller = "Pages", action = "Show" });

// Il backoffice resta raggiungibile su /dami tramite l'attribute routing
// esposto dalla Razor Class Library DAMIHeadlessCMS.Admin.

await DAMIHeadlessCMSIdentitySeeder.SeedAsync(app.Services, app.Configuration);

app.Run();