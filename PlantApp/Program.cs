using MaxRev.Gdal.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PlantApp.Components;
using PlantApp.Data;
using PlantApp.Models;
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
        sqlOptions => sqlOptions.UseNetTopologySuite())
    // Barn-entiteterna (Provyta/InventeringsProvyta) saknar egna query-filter, men
    // nås alltid via sina tenant-filtrerade föräldrar (Include), så isoleringen håller.
    .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

// Domäntjänster för shapefil-import och koordinattransformering.
builder.Services.AddScoped<IShapefileService, ShapefileService>();
builder.Services.AddScoped<IGeoPdfService, GeoPdfService>();
builder.Services.AddSingleton<ICoordinateTransformer, CoordinateTransformer>();

// --- Autentisering & auktorisering (lättviktig cookie-auth för demo) ---
// HttpContextAccessor krävs av auth-state-bryggan för att läsa den inloggade användaren under SSR.
builder.Services.AddHttpContextAccessor();

// Databasbaserad användarkatalog ovanpå ASP.NET Core Identity (hashade lösenord) och
// auth-state till Blazor-kretsen.
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<PlantAppDbContext>();

// Multi-tenant: aktuell organisation läses ur den inloggade användarens claim.
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

builder.Services.AddSingleton<UserDirectory>();
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

// Seeda roller och super-admin (idempotent) samt fyll användarkatalogens cache.
await SeedIdentityAsync(app.Services, app.Environment.IsProduction());

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

// --- Seedning av Identity-roller och super-admin (idempotent) ---
static async Task SeedIdentityAsync(IServiceProvider services, bool isProduction)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

    foreach (var roll in AppRoles.Alla)
    {
        if (!await roleManager.RoleExistsAsync(roll))
        {
            await roleManager.CreateAsync(new IdentityRole(roll));
        }
    }

    // Super-admin från konfiguration (SeedAdmin:Email / SeedAdmin:Password). Skapas bara
    // om det inte redan finns någon super-admin. OrganisationId = null (över alla organisationer).
    // I Development kommer värdena från User Secrets, i Production från miljövariabler
    // (SeedAdmin__Email / SeedAdmin__Password) eller motsvarande säker konfiguration –
    // aldrig från incheckade appsettings-filer.
    var superAdminEmail = config["SeedAdmin:Email"];
    var superAdminPassword = config["SeedAdmin:Password"];

    if (string.IsNullOrWhiteSpace(superAdminEmail) || string.IsNullOrWhiteSpace(superAdminPassword))
    {
        // Utan konfiguration seedas ingen super-admin. Varna tydligt i Production så att
        // en deploy inte oavsiktligt slutar utan inloggningsbar administratör.
        if (isProduction)
        {
            logger.LogWarning(
                "SeedAdmin:Email/Password saknas – ingen super-admin seedades. " +
                "Ange dem via miljövariabler (SeedAdmin__Email / SeedAdmin__Password).");
        }
    }
    else if (isProduction && superAdminPassword == "jacob123")
    {
        // Skyddsnät: den kända dev-hemligheten får aldrig användas i Production.
        throw new InvalidOperationException(
            "Det kända dev-lösenordet för SeedAdmin får inte användas i Production. " +
            "Sätt ett starkt lösenord via säker konfiguration (t.ex. miljövariabler).");
    }
    else if (await userManager.FindByEmailAsync(superAdminEmail) is null)
    {
        var superAdmin = new AppUser
        {
            UserName = superAdminEmail,
            Email = superAdminEmail,
            DisplayName = config["SeedAdmin:DisplayName"] ?? "Super Admin",
            Roll = AppRoles.SuperAdmin,
            OrganisationId = null,
            ArAktiv = true,
            SkapadDatum = DateTime.UtcNow,
        };

        var superResult = await userManager.CreateAsync(superAdmin, superAdminPassword);
        if (superResult.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdmin, AppRoles.SuperAdmin);
        }
        else
        {
            logger.LogError("Kunde inte skapa super-admin: {Fel}",
                string.Join("; ", superResult.Errors.Select(e => e.Description)));
        }
    }

    var directory = scope.ServiceProvider.GetRequiredService<UserDirectory>();
    await directory.RefreshCacheAsync();
}
