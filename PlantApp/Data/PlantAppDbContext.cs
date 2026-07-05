using Microsoft.EntityFrameworkCore;
using PlantApp.Models;

namespace PlantApp.Data;

public class PlantAppDbContext : DbContext
{
    public PlantAppDbContext(DbContextOptions<PlantAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Trakt> Trakter => Set<Trakt>();

    public DbSet<DelOmrade> DelOmraden => Set<DelOmrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trakt>(entity =>
        {
            entity.ToTable("Trakter");

            entity.HasKey(t => t.Id);

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

            entity.HasMany(t => t.DelOmraden)
                  .WithOne(d => d.Trakt)
                  .HasForeignKey(d => d.TraktId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DelOmrade>(entity =>
        {
            entity.ToTable("DelOmraden");

            entity.HasKey(d => d.Id);

            entity.Property(d => d.Areal)
                  .IsRequired();

            // Lagra enum som läsbar text i databasen i stället för int.
            entity.Property(d => d.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(d => d.Tradsort)
                  .HasMaxLength(100);

            entity.Property(d => d.AntalPlantor);
            entity.Property(d => d.AntalPlantorProvyta);

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
    }
}
