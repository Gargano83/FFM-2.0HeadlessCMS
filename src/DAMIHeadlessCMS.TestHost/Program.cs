using DAMIHeadlessCMS.Admin.Extensions;
using DAMIHeadlessCMS.Data.Extensions;
using DAMIHeadlessCMS.Data.Identity;
using DAMIHeadlessCMS.Scaffolding.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' non configurata. Copia appsettings.example.json in appsettings.json e valorizzala.");

// Stessa connection string per CmsDbContext (metadati in schema cms.*) e per
// il repository generico (tabelle applicative in dbo/FFM/TEST/ecc.): vivono
// nello stesso database fisico.
builder.Services.AddDAMIHeadlessCMSData(connectionString);
builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
builder.Services.AddDAMIHeadlessCMSScaffolding(connectionString);
builder.Services.AddDAMIHeadlessCMSIdentity();

builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

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
app.MapDefaultControllerRoute();

app.MapGet("/", () => Results.Redirect("/dami"));

await DAMIHeadlessCMSIdentitySeeder.SeedAsync(app.Services, app.Configuration);

app.Run();