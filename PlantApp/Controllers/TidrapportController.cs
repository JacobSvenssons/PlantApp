using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantApp.Data;
using PlantApp.Dtos;
using PlantApp.Models;

namespace PlantApp.Controllers;

[ApiController]
[Route("api/tidrapport")]
public class TidrapportController : ControllerBase
{
	private readonly PlantAppDbContext _db;

	public TidrapportController(PlantAppDbContext db)
	{
		_db = db;
	}

	/// <summary>
	/// Hämtar en persons tidrapportrader för en viss vecka (måndagsdatum).
	/// </summary>
	[HttpGet("mina")]
	public async Task<IActionResult> GetMina(
		[FromQuery] string anvandare,
		[FromQuery] DateTime vecka,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(anvandare))
		{
			return BadRequest("Användare saknas.");
		}

		var veckoStart = vecka.Date;

		var rader = await _db.Tidrapporter
			.AsNoTracking()
			.Include(t => t.Trakt)
			.Where(t => t.Anvandare == anvandare && t.VeckoStart == veckoStart)
			.OrderBy(t => t.Id)
			.Select(t => MapToDto(t))
			.ToListAsync(cancellationToken);

		return Ok(rader);
	}

	/// <summary>
	/// Skapar en ny tidrapportrad för en person.
	/// </summary>
	[HttpPost]
	public async Task<IActionResult> Skapa(
		[FromQuery] string anvandare,
		[FromBody] TidrapportInputDto dto,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(anvandare))
		{
			return BadRequest("Användare saknas.");
		}

		if (dto.AntalTimmar <= 0)
		{
			return BadRequest("Antal timmar måste vara större än 0.");
		}

		var tidrapport = new Tidrapport
		{
			Anvandare = anvandare,
			VeckoStart = dto.VeckoStart.Date,
			AntalTimmar = dto.AntalTimmar,
			ErsattningsTyp = dto.ErsattningsTyp,
			TraktId = dto.TraktId,
			Kommentar = string.IsNullOrWhiteSpace(dto.Kommentar) ? null : dto.Kommentar.Trim(),
			SkapadDatum = DateTime.UtcNow
		};

		_db.Tidrapporter.Add(tidrapport);
		await _db.SaveChangesAsync(cancellationToken);

		// Läs om med trakt för att kunna returnera namnet.
		await _db.Entry(tidrapport).Reference(t => t.Trakt).LoadAsync(cancellationToken);

		return Ok(MapToDto(tidrapport));
	}

	/// <summary>
	/// Tar bort en tidrapportrad. Endast ägaren får ta bort sin egen rad.
	/// </summary>
	[HttpDelete("{id:int}")]
	public async Task<IActionResult> TaBort(
		int id,
		[FromQuery] string anvandare,
		CancellationToken cancellationToken)
	{
		var tidrapport = await _db.Tidrapporter
			.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

		if (tidrapport is null)
		{
			return NotFound();
		}

		if (!string.Equals(tidrapport.Anvandare, anvandare, StringComparison.Ordinal))
		{
			return Forbid();
		}

		_db.Tidrapporter.Remove(tidrapport);
		await _db.SaveChangesAsync(cancellationToken);

		return NoContent();
	}

	/// <summary>
	/// Sammanställer timmar per person och ersättningsform, valfritt filtrerat på
	/// veckoperiod (<paramref name="fran"/>/<paramref name="till"/> avser veckans måndag).
	/// Underlag för ekonomi.
	/// </summary>
	[HttpGet("sammanstallning")]
	public async Task<IActionResult> GetSammanstallning(
		[FromQuery] DateTime? fran,
		[FromQuery] DateTime? till,
		CancellationToken cancellationToken)
	{
		var query = _db.Tidrapporter.AsNoTracking().AsQueryable();

		if (fran is { } f)
		{
			var franDatum = f.Date;
			query = query.Where(t => t.VeckoStart >= franDatum);
		}

		if (till is { } t)
		{
			var tillDatum = t.Date;
			query = query.Where(t => t.VeckoStart <= tillDatum);
		}

		var rader = await query.ToListAsync(cancellationToken);

		var summering = rader
			.GroupBy(t => t.Anvandare)
			.Select(g => new TidrapportSummeringDto
			{
				Anvandare = g.Key,
				TimmarHelackord = g.Where(x => x.ErsattningsTyp == ErsattningsTyp.HelAckord).Sum(x => x.AntalTimmar),
				TimmarDelackord = g.Where(x => x.ErsattningsTyp == ErsattningsTyp.DelAckord).Sum(x => x.AntalTimmar),
				TimmarTimpenning = g.Where(x => x.ErsattningsTyp == ErsattningsTyp.Timpenning).Sum(x => x.AntalTimmar)
			})
			.OrderBy(s => s.Anvandare)
			.ToList();

		return Ok(summering);
	}

	private static TidrapportDto MapToDto(Tidrapport t) => new()
	{
		Id = t.Id,
		Anvandare = t.Anvandare,
		VeckoStart = t.VeckoStart,
		AntalTimmar = t.AntalTimmar,
		ErsattningsTyp = t.ErsattningsTyp,
		TraktId = t.TraktId,
		TraktNamn = t.Trakt?.Namn,
		Kommentar = t.Kommentar
	};
}
