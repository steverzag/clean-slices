# Authentication

**Every endpoint requires a login.** A fallback policy requires an authenticated user wherever an endpoint doesn't say otherwise, so new features are protected without doing anything. A request without a valid token gets 401, and a logged-in user without the required role gets 403, both as ProblemDetails.

Only the health checks (`/health`, `/alive`, Development only) and the OpenAPI document allow anonymous access. To open an endpoint of your own, add `.AllowAnonymous()` in its `Register`:

```csharp
app.MapGet("/status", Handler).AllowAnonymous();
```

The shared code is in `Api/Configuration/Authentication/`; each provider adds its own folder there.

## Who is calling

`ICurrentUser` (`Infrastructure/CurrentUser/`) tells the code who made the request:

- `UserId` is the login's ID: the standard name identifier claim, which the token's subject (`sub`) fills. It is `null` without a login, and outside a request (background work such as the outbox).
- `IsAuthenticated` is `true` for a logged-in user.

Options that work per user read it, so they need no change: idempotency keys, for example, belong to the user who sent them.

The login ID is not the ID of an entity in your database. To link your own user records to logins, store the login ID on them (a `LoginId` column) and look it up with `ICurrentUser.UserId`.

## Roles

The provider's roles become standard role claims, so the usual ASP.NET Core checks work:

```csharp
app.MapDelete("/users/{id:guid}", Handler)
	.RequireAuthorization(policy => policy.RequireRole("admin"));
```

For a rule used on several endpoints, name a policy once and use the name:

```csharp
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("Admins", policy => policy.RequireRole("admin"));

app.MapDelete("/users/{id:guid}", Handler).RequireAuthorization("Admins");
```

Checks about a specific record ("only the task's owner can edit it") belong in the handler: load the record, compare its owner with `ICurrentUser.UserId`, and return 403 (or 404, to hide that it exists).

<!--#if (keycloak)-->
## Keycloak

The API validates the access tokens Keycloak issues (JWT bearer, `Microsoft.AspNetCore.Authentication.JwtBearer`). Keycloak is Apache 2.0.

### Running it locally

The AppHost runs Keycloak in a container (`quay.io/keycloak/keycloak`) at **`http://localhost:18080`**, a fixed port so the API, the `.http` file and your tools know where it is. It imports the realm in `aspire/CleanSlices.AppHost/Keycloak/realm.json` on every start:

| What | Value |
|---|---|
| Realm | `cleanslices-realm` |
| Client | `cleanslices-api`: public, authorization code with PKCE, and the password grant for tests and the `.http` file |
| Role | `admin` |
| Test users | `user` / `user`, and `admin` / `admin` with the `admin` role |
| Access tokens | valid for one hour |

Nothing else is kept: users and changes made in the admin console are gone after a restart. Change the realm file instead, or export the realm from the admin console and replace the file.

The admin console is at `http://localhost:18080/admin`, as the user `admin` of the *master* realm (a different user from the realm's `admin`). Its password is generated on the first run and kept in the AppHost's user secrets:

```
dotnet user-secrets list --project aspire/CleanSlices.AppHost
```

Look for `Parameters:cleanslices-keycloak-password`.

### Getting a token

The first request in `src/CleanSlices.Api/CleanSlices.Api.http` logs in the test user; the other requests send its token. From a shell:

```
curl -s -d "grant_type=password&client_id=cleanslices-api&username=user&password=user" \
  http://localhost:18080/realms/cleanslices-realm/protocol/openid-connect/token
```

The OpenAPI document describes the login (OAuth2, authorization code), so API reference pages can sign in through Keycloak.

### Settings

The defaults in code (`KeycloakOptions`) match the AppHost's Keycloak; `appsettings.json` doesn't need them:

| Setting | Default | |
|---|---|---|
| `Authentication:Keycloak:Authority` | `http://localhost:18080/realms/cleanslices-realm` | The realm's URL; tokens must have been issued by it |
| `Authentication:Keycloak:Audience` | `cleanslices-api` | Tokens must name it in `aud`; the realm's client adds it with an audience mapper |

Outside Development the authority must use HTTPS.

**With your own Keycloak**, set the authority to your realm, and make sure its access tokens carry the audience: add an *Audience* mapper to the client (or a client scope) that includes `cleanslices-api`. Realm roles come from the token's `realm_access.roles`, which the default `roles` client scope adds. Client roles (`resource_access`) aren't read; map them as realm roles, or read them the same way in `KeycloakAuthentication`.

### Production notes

- The password grant is enabled on the realm's client only for tests and local requests; turn it off (`directAccessGrantsEnabled`) in a real realm. Browser apps use the authorization code flow with PKCE.
- `start-dev` uses an in-memory database and HTTP. A real Keycloak runs with `start`, its own database and HTTPS.
<!--#endif-->

## Tests

The tests log in for you: `ApiFactory` gets a token for the test user once per run and adds it to every client it creates, so endpoint tests need nothing extra. For another user, call `factory.LoginAsync(TestUsers.Admin)` and set the `Authorization` header yourself. To check that an endpoint requires a login, remove the header:

```csharp
var client = factory.CreateClient();
client.DefaultRequestHeaders.Authorization = null;
```

<!--#if (keycloak)-->
The tests run Keycloak in a container (Testcontainers) with the same realm file as the AppHost.
<!--#endif-->
