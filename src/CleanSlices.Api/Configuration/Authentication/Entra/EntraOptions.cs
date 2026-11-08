#if (entra)
namespace CleanSlices.Api.Configuration.Authentication.Entra;

// TenantId and ClientId come from your app registration; the AppHost asks for them.
public sealed class EntraOptions
{
	public const string Section = "Authentication:Entra";

	public string Instance { get; set; } = "https://login.microsoftonline.com/";
	public string TenantId { get; set; } = "";
	public string ClientId { get; set; } = "";
	public string Scope { get; set; } = "access_as_user";

	public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";

	// Entra issues v1 or v2 tokens depending on the app registration, and names the API by its client ID or its URI.
	public string[] Issuers => [Authority, $"https://sts.windows.net/{TenantId}/"];
	public string[] Audiences => [ClientId, $"api://{ClientId}"];
}
#endif
