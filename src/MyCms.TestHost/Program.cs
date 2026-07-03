using MyCms.Admin.Extensions;
using MyCms.Data.Extensions;
using MyCms.Scaffolding.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' non configurata. Copia appsettings.example.json in appsettings.json e valorizzala.");

// Stessa connection string per CmsDbContext (metadati in schema cms.*) e per
// il repository generico (tabelle applicative in dbo/FFM/TEST/ecc.): vivono
// nello stesso database fisico.
builder.Services.AddMyCmsData(connectionString);
builder.Services.AddMyCmsAdmin(connectionString);
builder.Services.AddMyCmsScaffolding(connectionString);

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
app.UseAuthorization();

app.MapControllers();
app.MapDefaultControllerRoute();

app.MapGet("/", () => Results.Redirect("/backoffice/admin"));

app.Run();