namespace PlantApp.Dtos;

/// <summary>
/// Payload för att sätta traktens standardträdsort (admin-funktion i kartvyn).
/// </summary>
public class TraktTradsortDto
{
	/// <summary>Standardträdsort för trakten. Null tömmer standarden.</summary>
	public string? StandardTradsort { get; set; }
}
