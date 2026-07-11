namespace PlantApp.Models;

/// <summary>
/// En inventeringsomgångs status genom uppföljningsflödet. En omgång påbörjas i fält
/// och markeras klar när alla provytor är tagna.
/// </summary>
public enum InventeringStatus
{
	Paborjad = 0,
	Klar = 1
}
