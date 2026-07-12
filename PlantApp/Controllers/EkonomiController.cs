using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantApp.Data;
using PlantApp.Dtos;
using PlantApp.Models;

namespace PlantApp.Controllers;

[ApiController]
[Route("api/ekonomi")]
public class EkonomiController : ControllerBase
{
	private readonly PlantAppDbContext _db;

	public EkonomiController(PlantAppDbContext db)
	{
		_db = db;
	}

	/// <summary>
	/// Sammanställer registrerade plantor (och framtida timmar) per delområde, som underlag
	/// för ekonomi/lön. Kan filtreras på registreringsperiod via <paramref name="fran"/> och
	/// <paramref name="till"/> (inklusiva datum). Delområden utan registreringsdatum tas
	/// bara med när inget periodfilter anges.
	/// </summary>
	[HttpGet("sammanstallning")]
	public async Task<IActionResult> GetSammanstallning(
		[FromQuery] DateTime? fran,
		[FromQuery] DateTime? till,
		CancellationToken cancellationToken)
	{
		var installning = await HamtaEllerSkapaInstallning(cancellationToken);
		var standard = installning.StandardErsattningsTyp;

		var delomraden = await _db.DelOmraden
			.AsNoTracking()
			.Include(d => d.Trakt)
			.Where(d => d.AntalPlantor != null && d.AntalPlantor > 0)
			.ToListAsync(cancellationToken);

		// Periodfilter (inklusivt). Till-datumet utökas till slutet av dygnet.
		if (fran is { } franDatum)
		{
			delomraden = delomraden
				.Where(d => d.RegistreradDatum is { } dt && dt >= franDatum.Date)
				.ToList();
		}

		if (till is { } tillDatum)
		{
			var tillSlut = tillDatum.Date.AddDays(1);
			delomraden = delomraden
				.Where(d => d.RegistreradDatum is { } dt && dt < tillSlut)
				.ToList();
		}

		var rader = delomraden
			.OrderBy(d => d.Trakt.Namn)
			.ThenBy(d => d.Id)
			.Select(d =>
			{
				var effektiv = d.ErsattningsTyp ?? d.Trakt.ErsattningsTyp ?? standard;
				var arOverride = d.ErsattningsTyp is not null || d.Trakt.ErsattningsTyp is not null;

				return new EkonomiRadDto
				{
					TraktId = d.TraktId,
					TraktNamn = d.Trakt.Namn,
					Plantsattare = string.IsNullOrWhiteSpace(d.TilldeladAnvandare) ? "Ej tilldelad" : d.TilldeladAnvandare,
					DelOmradeId = d.Id,
					Tradsort = d.Tradsort ?? d.Trakt.StandardTradsort,
					Areal = d.Areal,
					AntalPlantor = d.AntalPlantor ?? 0,
					AntalTimmar = null,
					RegistreradDatum = d.RegistreradDatum,
					ErsattningsTyp = effektiv,
					ErsattningsTypArOverride = arOverride,
					DelOmradeErsattningsTyp = d.ErsattningsTyp
				};
			})
			.ToList();

		return Ok(rader);
	}

	/// <summary>
	/// Hämtar den globala ekonomiinställningen (standardersättningsform).
	/// </summary>
	[HttpGet("installning")]
	public async Task<IActionResult> GetInstallning(CancellationToken cancellationToken)
	{
		var installning = await HamtaEllerSkapaInstallning(cancellationToken);

		return Ok(new EkonomiInstallningDto
		{
			StandardErsattningsTyp = installning.StandardErsattningsTyp
		});
	}

	/// <summary>
	/// Uppdaterar den globala standardersättningsformen.
	/// </summary>
	[HttpPut("installning")]
	public async Task<IActionResult> UpdateInstallning(
		[FromBody] EkonomiInstallningDto dto,
		CancellationToken cancellationToken)
	{
		var installning = await HamtaEllerSkapaInstallning(cancellationToken);
		installning.StandardErsattningsTyp = dto.StandardErsattningsTyp;
		await _db.SaveChangesAsync(cancellationToken);

		return Ok(new EkonomiInstallningDto
		{
			StandardErsattningsTyp = installning.StandardErsattningsTyp
		});
	}

	/// <summary>
	/// Sätter eller rensar ersättningsform-override för en trakt.
	/// </summary>
	[HttpPut("trakt/{traktId:int}/ersattningstyp")]
	public async Task<IActionResult> SetTraktErsattningsTyp(
		int traktId,
		[FromBody] ErsattningsTypOverrideDto dto,
		CancellationToken cancellationToken)
	{
		var trakt = await _db.Trakter.FirstOrDefaultAsync(t => t.Id == traktId, cancellationToken);

		if (trakt is null)
		{
			return NotFound();
		}

		trakt.ErsattningsTyp = dto.ErsattningsTyp;
		await _db.SaveChangesAsync(cancellationToken);

		return NoContent();
	}

	/// <summary>
	/// Sätter eller rensar ersättningsform-override för ett delområde.
	/// </summary>
	[HttpPut("delomrade/{delOmradeId:int}/ersattningstyp")]
	public async Task<IActionResult> SetDelOmradeErsattningsTyp(
		int delOmradeId,
		[FromBody] ErsattningsTypOverrideDto dto,
		CancellationToken cancellationToken)
	{
		var delOmrade = await _db.DelOmraden.FirstOrDefaultAsync(d => d.Id == delOmradeId, cancellationToken);

		if (delOmrade is null)
		{
			return NotFound();
		}

		delOmrade.ErsattningsTyp = dto.ErsattningsTyp;
		await _db.SaveChangesAsync(cancellationToken);

		return NoContent();
	}

	/// <summary>
	/// Läser den enda inställningsraden (Id = 1) och skapar den om den saknas.
	/// </summary>
	private async Task<EkonomiInstallning> HamtaEllerSkapaInstallning(CancellationToken cancellationToken)
	{
		var installning = await _db.EkonomiInstallningar
			.OrderBy(e => e.Id)
			.FirstOrDefaultAsync(cancellationToken);

		if (installning is null)
		{
			installning = new EkonomiInstallning
			{
				StandardErsattningsTyp = ErsattningsTyp.HelAckord
			};
			_db.EkonomiInstallningar.Add(installning);
			await _db.SaveChangesAsync(cancellationToken);
		}

		return installning;
	}
}
