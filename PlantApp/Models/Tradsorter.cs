namespace PlantApp.Models;

/// <summary>
/// Standardalternativ för trädsort som kan väljas i appen. Hålls medvetet enkel som
/// en statisk lista tills trädsorter eventuellt behöver konfigureras i databasen.
/// </summary>
public static class Tradsorter
{
	/// <summary>Valbara trädsorter i den ordning de visas i dropdowns.</summary>
	public static readonly IReadOnlyList<string> Standard =
	[
		"Gran",
		"Tall",
		"Contorta"
	];
}
