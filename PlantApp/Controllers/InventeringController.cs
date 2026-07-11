using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using PlantApp.Data;
using PlantApp.Dtos;
using PlantApp.Models;
using PlantApp.Services;

namespace PlantApp.Controllers;

[ApiController]
[Route("api/inventering")]
public class InventeringController : ControllerBase
{
	private readonly PlantAppDbContext _db;
	private readonly ICoordinateTransformer _transformer;

	public InventeringController(PlantAppDbContext db, ICoordinateTransformer transformer)
	{
		_db = db;
		_transformer = transformer;
	}

	/// <summary>
	/// Listar inventeringsomgångar. Filtrera valfritt på trakt (<paramref name="traktId"/>).
	/// </summary>
	[HttpGet]
	public async Task<IActionResult> GetInventeringar(
		[FromQuery] int? traktId,
		CancellationToken cancellationToken)
	{
		var query = _db.Inventeringar
			.AsNoTracking()
			.Include(i => i.Trakt)
			.Include(i => i.Provytor)
			.AsQueryable();

		if (traktId is { } tid)
		{
			query = query.Where(i => i.TraktId == tid);
		}

		var inventeringar = await query
			.OrderByDescending(i => i.SkapadDatum)
			.Select(i => new InventeringDto
			{
				Id = i.Id,
				TraktId = i.TraktId,
				TraktNamn = i.Trakt.Namn,
				SkapadDatum = i.SkapadDatum,
				TilldeladAnvandare = i.TilldeladAnvandare,
				Status = i.Status,
				AntalProvytor = i.Provytor.Count,
				TotaltAntalPlantor = i.Provytor.Sum(p => p.AntalPlantor)
			})
			.ToListAsync(cancellationToken);

		return Ok(inventeringar);
	}

	/// <summary>
	/// Returnerar en inventeringsomgång med dess provytor (för inventerarvyn).
	/// </summary>
	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetInventering(int id, CancellationToken cancellationToken)
	{
		var inventering = await _db.Inventeringar
			.AsNoTracking()
			.Include(i => i.Trakt)
			.Include(i => i.Provytor)
			.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

		if (inventering is null)
		{
			return NotFound();
		}

		return Ok(MapToDetail(inventering));
	}

	/// <summary>
	/// Skapar en ny inventeringsomgång för en trakt.
	/// </summary>
	[HttpPost]
	public async Task<IActionResult> CreateInventering(
		[FromBody] InventeringCreateDto dto,
		CancellationToken cancellationToken)
	{
		var trakt = await _db.Trakter
			.FirstOrDefaultAsync(t => t.Id == dto.TraktId, cancellationToken);

		if (trakt is null)
		{
			return BadRequest("Trakten finns inte.");
		}

		var inventering = new Inventering
		{
			TraktId = dto.TraktId,
			SkapadDatum = DateTime.UtcNow,
			TilldeladAnvandare = Normalisera(dto.TilldeladAnvandare),
			Status = InventeringStatus.Paborjad
		};

		_db.Inventeringar.Add(inventering);
		await _db.SaveChangesAsync(cancellationToken);

		// Läs in trakten för namnet i svaret.
		await _db.Entry(inventering).Reference(i => i.Trakt).LoadAsync(cancellationToken);

		return CreatedAtAction(
			nameof(GetInventering),
			new { id = inventering.Id },
			MapToDetail(inventering));
	}

	/// <summary>
	/// Hämtar eller skapar (lazy) inventeringsomgången för en trakt. Används när en
	/// inventerare öppnar en trakt som är öppnad för inventering på startsidan.
	/// Om trakten inte är öppnad för inventering nekas anropet.
	/// </summary>
	[HttpPost("starta")]
	public async Task<IActionResult> StartaForTrakt(
		[FromBody] InventeringCreateDto dto,
		CancellationToken cancellationToken)
	{
		var trakt = await _db.Trakter
			.FirstOrDefaultAsync(t => t.Id == dto.TraktId, cancellationToken);

		if (trakt is null)
		{
			return BadRequest("Trakten finns inte.");
		}

		if (!trakt.InventeringMojlig)
		{
			return BadRequest("Trakten är inte öppnad för inventering.");
		}

		var anvandare = Normalisera(dto.TilldeladAnvandare);

		// Återanvänd en befintlig omgång för användaren (eller trakten) om den finns –
		// annars skapa en ny. Det gör att flödet blir "öppna och börja" utan dubbletter.
		var inventering = await _db.Inventeringar
			.Include(i => i.Trakt)
			.Include(i => i.Provytor)
			.Where(i => i.TraktId == dto.TraktId)
			.Where(i => anvandare == null || i.TilldeladAnvandare == anvandare)
			.OrderByDescending(i => i.SkapadDatum)
			.FirstOrDefaultAsync(cancellationToken);

		if (inventering is null)
		{
			inventering = new Inventering
			{
				TraktId = dto.TraktId,
				SkapadDatum = DateTime.UtcNow,
				TilldeladAnvandare = anvandare,
				Status = InventeringStatus.Paborjad
			};

			_db.Inventeringar.Add(inventering);
			await _db.SaveChangesAsync(cancellationToken);

			await _db.Entry(inventering).Reference(i => i.Trakt).LoadAsync(cancellationToken);
		}

		return Ok(MapToDetail(inventering));
	}

	/// <summary>
	/// Sparar en inventeringsomgångs provytor (ersätter hela listan).
	/// </summary>
	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateInventering(
		int id,
		[FromBody] InventeringUpdateDto dto,
		CancellationToken cancellationToken)
	{
		var inventering = await _db.Inventeringar
			.Include(i => i.Trakt)
			.Include(i => i.Provytor)
			.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

		if (inventering is null)
		{
			return NotFound();
		}

		// Provytor skickas som en fullständig lista – ersätt de befintliga raderna.
		_db.RemoveRange(inventering.Provytor);
		inventering.Provytor = dto.Provytor
			.Select(p => new InventeringsProvyta
			{
				InventeringId = inventering.Id,
				DelOmradeId = p.DelOmradeId,
				AntalPlantor = p.AntalPlantor,
				Tradsort = Normalisera(p.Tradsort),
				Kommentar = Normalisera(p.Kommentar),
				// Kartklicket kommer i WGS84 (long/lat) – lagra som punkt i SWEREF99 TM.
				Plats = p.Latitud is { } lat && p.Longitud is { } lon
					? _transformer.ToSweref99Tm(lon, lat)
					: null
			})
			.ToList();

		await _db.SaveChangesAsync(cancellationToken);

		return Ok(MapToDetail(inventering));
	}

	/// <summary>
	/// Raderar en inventeringsomgång och – via cascade – dess provytor.
	/// </summary>
	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteInventering(int id, CancellationToken cancellationToken)
	{
		var inventering = await _db.Inventeringar
			.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

		if (inventering is null)
		{
			return NotFound();
		}

		_db.Inventeringar.Remove(inventering);
		await _db.SaveChangesAsync(cancellationToken);

		return NoContent();
	}

	/// <summary>
	/// Returnerar en inventeringsomgångs provytor som GeoJSON-punkter (WGS84) för
	/// webbkartan och för admins nedladdning. Endast provytor med en plats tas med.
	/// </summary>
	[HttpGet("{id:int}/provytor/geojson")]
	public async Task<IActionResult> GetProvytorGeoJson(int id, CancellationToken cancellationToken)
	{
		var inventering = await _db.Inventeringar
			.AsNoTracking()
			.Include(i => i.Provytor)
			.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

		if (inventering is null)
		{
			return NotFound();
		}

		var features = new FeatureCollection();
		foreach (var provyta in inventering.Provytor)
		{
			if (provyta.Plats is null)
			{
				continue;
			}

			// Lagrat i SWEREF99 TM (3006) -> reprojicera till WGS84 (4326) för kartan.
			var wgs84 = _transformer.ToWgs84(provyta.Plats);

			var attributes = new AttributesTable
			{
				{ "id", provyta.Id },
				{ "inventeringId", provyta.InventeringId },
				{ "delOmradeId", provyta.DelOmradeId },
				{ "antalPlantor", provyta.AntalPlantor },
				{ "tradsort", provyta.Tradsort },
				{ "kommentar", provyta.Kommentar }
			};

			features.Add(new Feature(wgs84, attributes));
		}

		var geoJson = new GeoJsonWriter().Write(features);
		return Content(geoJson, "application/json");
	}

	private InventeringDetailDto MapToDetail(Inventering inventering) =>
		new()
		{
			Id = inventering.Id,
			TraktId = inventering.TraktId,
			TraktNamn = inventering.Trakt?.Namn,
			SkapadDatum = inventering.SkapadDatum,
			TilldeladAnvandare = inventering.TilldeladAnvandare,
			Status = inventering.Status,
			Provytor = inventering.Provytor
				.OrderBy(p => p.Id)
				.Select(p =>
				{
					var wgs84 = p.Plats is not null ? _transformer.ToWgs84(p.Plats) : null;
					return new InventeringsProvytaDto
					{
						Id = p.Id,
						DelOmradeId = p.DelOmradeId,
						AntalPlantor = p.AntalPlantor,
						Tradsort = p.Tradsort,
						Kommentar = p.Kommentar,
						Latitud = wgs84?.Y,
						Longitud = wgs84?.X
					};
				})
				.ToList()
		};

	private static string? Normalisera(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
