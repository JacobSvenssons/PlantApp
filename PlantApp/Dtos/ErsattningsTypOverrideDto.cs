using PlantApp.Models;

namespace PlantApp.Dtos;

/// <summary>
/// Payload för att sätta/rensa ersättningsform-override på en trakt eller ett delområde.
/// Null <see cref="ErsattningsTyp"/> rensar override så att arv gäller igen.
/// </summary>
public class ErsattningsTypOverrideDto
{
	public ErsattningsTyp? ErsattningsTyp { get; set; }
}
