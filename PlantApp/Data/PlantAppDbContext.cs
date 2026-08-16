using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PlantApp.Models;
using PlantApp.Services;

namespace PlantApp.Data;

public class PlantAppDbContext : IdentityDbContext<AppUser>
{
    private readonly ITenantProvider _tenantProvider;

    public PlantAppDbContext(DbContextOptions<PlantAppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Organisation> Organisationer => Set<Organisation>();

    public DbSet<Trakt> Trakter => Set<Trakt>();

    public DbSet<DelOmrade> DelOmraden => Set<DelOmrade>();

    public DbSet<Provyta> Provytor => Set<Provyta>();

    public DbSet<Inventering> Inventeringar => Set<Inventering>();

    public DbSet<InventeringsProvyta> InventeringsProvytor => Set<InventeringsProvyta>();

    public DbSet<EkonomiInstallning> EkonomiInstallningar => Set<EkonomiInstallning>();

    public DbSet<Tidrapport> Tidrapporter => Set<Tidrapport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organisation>(entity =>
        {
            entity.ToTable("Organisationer");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Namn)
                  .IsRequired()
                  .HasMaxLength(200);
            entity.Property(o => o.SkapadDatum).IsRequired();
            entity.Property(o => o.ArAktiv).IsRequired();
        });

        // --- Multi-tenant global query filters ---
        // Super-admin (OrganisationId == null) släpper igenom alla organisationers data.
        // Övriga ser bara sin egen organisation. AppUser filtreras också; UserDirectory
        // använder IgnoreQueryFilters() där super-admin/seed behöver se över gränser.
        modelBuilder.Entity<AppUser>()
            .HasQueryFilter(u => _tenantProvider.OrganisationId == null || u.OrganisationId == _tenantProvider.OrganisationId);
        modelBuilder.Entity<Trakt>()
            .HasQueryFilter(t => _tenantProvider.OrganisationId == null || t.OrganisationId == _tenantProvider.OrganisationId);
        modelBuilder.Entity<DelOmrade>()
            .HasQueryFilter(d => _tenantProvider.OrganisationId == null || d.OrganisationId == _tenantProvider.OrganisationId);
        modelBuilder.Entity<Inventering>()
            .HasQueryFilter(i => _tenantProvider.OrganisationId == null || i.OrganisationId == _tenantProvider.OrganisationId);
        modelBuilder.Entity<Tidrapport>()
            .HasQueryFilter(t => _tenantProvider.OrganisationId == null || t.OrganisationId == _tenantProvider.OrganisationId);
        modelBuilder.Entity<EkonomiInstallning>()
            .HasQueryFilter(e => _tenantProvider.OrganisationId == null || e.OrganisationId == _tenantProvider.OrganisationId);

        modelBuilder.Entity<Trakt>(entity =>
        {
            entity.ToTable("Trakter");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.OrganisationId)
                  .IsRequired();

            entity.HasIndex(t => t.OrganisationId);

            entity.Property(t => t.Namn)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(t => t.SkapadDatum)
                  .IsRequired();

            // Lagra enum som läsbar text i databasen i stället för int.
            entity.Property(t => t.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(t => t.StandardTradsort)
                  .HasMaxLength(100);

            entity.Property(t => t.MalPlantorPerHektar);

            entity.Property(t => t.InventeringMojlig)
                  .IsRequired()
                  .HasDefaultValue(false);

            // Ersättningsform (override) lagras som läsbar text; null = ärv global/trakt.
            entity.Property(t => t.ErsattningsTyp)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasMany(t => t.DelOmraden)
                  .WithOne(d => d.Trakt)
                  .HasForeignKey(d => d.TraktId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DelOmrade>(entity =>
        {
            entity.ToTable("DelOmraden");

            entity.HasKey(d => d.Id);

            entity.Property(d => d.OrganisationId)
                  .IsRequired();

            entity.HasIndex(d => d.OrganisationId);

            entity.Property(d => d.Areal)
                  .IsRequired();

            // Lagra enum som läsbar text i databasen i stället för int.
            entity.Property(d => d.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(d => d.Tradsort)
                  .HasMaxLength(100);

            entity.Property(d => d.MalPlantorPerHektar);

            entity.Property(d => d.AntalPlantor);

            entity.Property(d => d.RegistreradDatum);

            // Ersättningsform (override) lagras som läsbar text; null = ärv trakt/global.
            entity.Property(d => d.ErsattningsTyp)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(d => d.Kommentar)
                  .HasMaxLength(1000);

            entity.Property(d => d.TilldeladAnvandare)
                  .HasMaxLength(100);

            entity.HasMany(d => d.Provytor)
                  .WithOne(p => p.DelOmrade)
                  .HasForeignKey(p => p.DelOmradeId)
                  .OnDelete(DeleteBehavior.Cascade);

            // --- Spatial-mappning ---
            // Polygonen lagras nativt i SQL Server. SWEREF99 TM (SRID 3006) är ett
            // projicerat (planärt) koordinatsystem, därför används kolumntypen 'geometry'
            // och INTE 'geography' (som kräver ett geografiskt SRID, t.ex. 4326/WGS84).
            entity.Property(d => d.Geometri)
                  .HasColumnType("geometry");

            // Obs! Ett spatialt index kan inte skapas via HasIndex för SQL Server.
            // Lägg till det manuellt i en migration (Up/Down), t.ex.:
            //   migrationBuilder.Sql(@"
            //       CREATE SPATIAL INDEX SIX_DelOmraden_Geometri
            //       ON dbo.DelOmraden(Geometri)
            //       USING GEOMETRY_AUTO_GRID
            //       WITH (BOUNDING_BOX = (200000, 6100000, 1000000, 7700000));"); // SWEREF99 TM (SRID 3006), meter
        });

        modelBuilder.Entity<Provyta>(entity =>
        {
            entity.ToTable("Provytor");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.AntalPlantor)
                  .IsRequired();

            // Valfri provyta-plats lagras nativt som en punkt i SWEREF99 TM (SRID 3006).
            // Precis som DelOmrade.Geometri används kolumntypen 'geometry' (projicerat CRS).
            entity.Property(p => p.Plats)
                  .HasColumnType("geometry");
        });

        modelBuilder.Entity<Inventering>(entity =>
        {
            entity.ToTable("Inventeringar");

            entity.HasKey(i => i.Id);

            entity.Property(i => i.OrganisationId)
                  .IsRequired();

            entity.HasIndex(i => i.OrganisationId);

            entity.Property(i => i.SkapadDatum)
                  .IsRequired();

            entity.Property(i => i.TilldeladAnvandare)
                  .HasMaxLength(100);

            // Lagra enum som läsbar text i databasen i stället för int.
            entity.Property(i => i.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasOne(i => i.Trakt)
                  .WithMany()
                  .HasForeignKey(i => i.TraktId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.Provytor)
                  .WithOne(p => p.Inventering)
                  .HasForeignKey(p => p.InventeringId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventeringsProvyta>(entity =>
        {
            entity.ToTable("InventeringsProvytor");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.AntalPlantor)
                  .IsRequired();

            entity.Property(p => p.Tradsort)
                  .HasMaxLength(100);

            entity.Property(p => p.Kommentar)
                  .HasMaxLength(1000);

            // Valfri provyta-plats lagras nativt som en punkt i SWEREF99 TM (SRID 3006).
            entity.Property(p => p.Plats)
                  .HasColumnType("geometry");
        });

        modelBuilder.Entity<EkonomiInstallning>(entity =>
        {
            entity.ToTable("EkonomiInstallningar");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.OrganisationId)
                  .IsRequired();

            entity.HasIndex(e => e.OrganisationId)
                  .IsUnique();

            // Lagra enum som läsbar text i databasen i stället för int.
            entity.Property(e => e.StandardErsattningsTyp)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            // En rad per organisation seedas när organisationen skapas (inte här).
        });

        modelBuilder.Entity<Tidrapport>(entity =>
        {
            entity.ToTable("Tidrapporter");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.OrganisationId)
                  .IsRequired();

            entity.HasIndex(t => t.OrganisationId);

            entity.Property(t => t.Anvandare)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(t => t.VeckoStart)
                  .IsRequired();

            entity.Property(t => t.AntalTimmar)
                  .IsRequired();

            // Lagra enum som läsbar text i databasen i stället för int.
            entity.Property(t => t.ErsattningsTyp)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(t => t.Kommentar)
                  .HasMaxLength(1000);

            entity.Property(t => t.SkapadDatum)
                  .IsRequired();

            // Valfri trakt-koppling; om trakten raderas nollställs kopplingen.
            entity.HasOne(t => t.Trakt)
                  .WithMany()
                  .HasForeignKey(t => t.TraktId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(t => new { t.Anvandare, t.VeckoStart });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampaOrganisation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampaOrganisation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stämplar automatiskt aktuell organisation på nya tenant-ägda rader som ännu
    /// saknar en (OrganisationId == 0). Super-admin (null tenant) måste sätta
    /// OrganisationId explicit innan spar.
    /// </summary>
    private void StampaOrganisation()
    {
        var tenantId = _tenantProvider.OrganisationId;
        if (tenantId is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Trakt t when t.OrganisationId == 0:
                    t.OrganisationId = tenantId.Value;
                    break;
                case DelOmrade d when d.OrganisationId == 0:
                    d.OrganisationId = tenantId.Value;
                    break;
                case Inventering i when i.OrganisationId == 0:
                    i.OrganisationId = tenantId.Value;
                    break;
                case Tidrapport tr when tr.OrganisationId == 0:
                    tr.OrganisationId = tenantId.Value;
                    break;
                case EkonomiInstallning e when e.OrganisationId == 0:
                    e.OrganisationId = tenantId.Value;
                    break;
            }
        }
    }
}