namespace PlantApp.Dtos;

/// <summary>
/// Payload för att öppna eller stänga en trakt för inventering (admin-funktion).
/// </summary>
public class SetInventeringMojligDto
{
	/// <summary>True öppnar trakten för inventering, false stänger den.</summary>
	public bool Mojlig { get; set; }
}
