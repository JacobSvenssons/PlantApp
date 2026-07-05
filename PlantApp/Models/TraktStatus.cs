namespace PlantApp.Models;

/// <summary>
/// Traktens status genom planterings- och inskickningsflödet.
/// </summary>
public enum TraktStatus
{
    Planerad = 0,
    Paborjad = 1,
    Klar = 2,
    Inskickad = 3
}
