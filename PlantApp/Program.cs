using MaxRev.Gdal.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PlantApp.Components;
using PlantApp.Data;
using PlantApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Bakom en reverse proxy (t.ex. Ngrok) kommer trafiken in som HTTP mot localhost,
// medan användaren surfar via https://...ngrok. Utan detta vet appen inte sin
// riktiga utåtriktade adress/schema, vilket ger felaktiga redirects efter inloggning
// ("null går inte att nå" i mobilen). Vi litar på X-Forwarded-For/Proto från proxyn.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Ngrok-värdarna är inte kända i förväg – rensa default-listorna så headers accepteras.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Initiera GDAL/OGR en gång vid uppstart och registrera alla drivrutiner (inkl. PDF).
// Krävs innan GeoPdfService kan öppna GeoPDF-filer.
GdalBase.ConfigureAll();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Web API-controllers (t.ex. TraktController för shapefil-import och GeoJSON-export).
builder.Services.AddControllers();

// HttpClient för att kartvyn (Blazor Server) ska kunna hämta GeoJSON och göra PUT mot vårt eget API.
builder.Services.AddHttpClient();

// Registrera EF Core mot SQL Server med spatial-stöd (NetTopologySuite).
builder.Services.AddDbContext<PlantAppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PlantAppDb"),
        sqlOptions => sqlOptions.UseNetTopologySuite()));

// Domäntjänster för shapefil-import och koordinattransformering.
builder.Services.AddScoped<IShapefileService, ShapefileService>();
builder.Services.AddScoped<IGeoPdfService, GeoPdfService>();
builder.Services.AddSingleton<ICoordinateTransformer, CoordinateTransformer>();

// --- Autentisering & auktorisering (lättviktig cookie-auth för demo) ---
// HttpContextAccessor krävs av auth-state-bryggan för att läsa den inloggade användaren under SSR.
builder.Services.AddHttpContextAccessor();

// Seedad demo-användarlagring (admin/plantbas/plantsattare) och auth-state till Blazor-kretsen.
builder.Services.AddSingleton<DemoUserStore>();
builder.Services.AddScoped<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "PlantApp.Auth";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Skapa/uppdatera databasen automatiskt i utvecklingsläge så att migreringar
// appliceras vid uppstart. Undviker "Cannot open database 'PlantAppDb'" på en ny
// maskin där databasen ännu inte har skapats.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PlantAppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
// Måste ligga tidigast av allt så att efterföljande middleware ser rätt schema/host
// från proxyn (Ngrok). Annars blir HTTPS-redirect och cookie-scope fel.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Låt kataloganrop som "/falt/" servera "/falt/index.html". MapStaticAssets serverar
// inte standarddokument automatiskt, så utan detta svarar servern 404 (→ /not-found)
// när man öppnar den fristående offline-fältytan.
app.UseDefaultFiles();

app.MapStaticAssets();
app.MapControllers();

// Logga ut: nollställ auth-cookien och gå till startsidan.
app.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
