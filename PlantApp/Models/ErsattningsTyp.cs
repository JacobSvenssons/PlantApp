namespace PlantApp.Models;

/// <summary>
/// Ersättningsform för planteringsarbete. Används som underlag för ekonomi/lön –
/// appen räknar inte ut ersättning utan visar bara vilken form som gäller.
/// </summary>
public enum ErsattningsTyp
{
	/// <summary>Helackord – ersättning helt baserad på antal satta plantor.</summary>
	HelAckord = 0,

	/// <summary>Delackord – kombination av grundtimlön och ackord per planta.</summary>
	DelAckord = 1,

	/// <summary>Timpenning – ersättning per arbetad timme, t.ex. vid hjälpplantering.</summary>
	Timpenning = 2
}
