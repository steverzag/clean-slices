#if (keycloak)
namespace CleanSlices.Api.Configuration.Authentication.Keycloak;

// The defaults match the Keycloak the AppHost runs; set Authentication:Keycloak:Authority for any other.
public sealed class KeycloakOptions
{
	public const string Section = "Authentication:Keycloak";

	public string Authority { get; set; } = "http://localhost:18080/realms/cleanslices-realm";
	public string Audience { get; set; } = "cleanslices-api";
}
#endif
