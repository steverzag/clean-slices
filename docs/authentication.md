# Authentication

**Every endpoint requires a login.** A fallback policy requires an authenticated user wherever an endpoint doesn't say otherwise, so new features are protected without doing anything. A request without a valid token gets 401, and a logged-in user without the required role gets 403, both as ProblemDetails.

Only the health checks (`/health`, `/alive`, Development only) and the OpenAPI document allow anonymous access. To open an endpoint of your own, add `.AllowAnonymous()` in its `Register`:

```csharp
app.MapGet("/status", Handler).AllowAnonymous();
```

The shared code is in `Api/Configuration/Authentication/`; each provider adds its own folder there.

## Who is calling

`ICurrentUser` (`Infrastructure/CurrentUser/`) tells the code who made the request:

<!--#if (entra)-->
- `UserId` is the login's ID: the standard name identifier claim, which holds the user's object ID (`oid`) in the tenant. Entra's subject (`sub`) is different for every app, so it isn't used. It is `null` without a login, and outside a request (background work such as the outbox).
<!--#elseif (identity)-->
- `UserId` is the login's ID: the standard name identifier claim, which holds the account's ID in `AspNetUsers`. It is `null` without a login, and outside a request (background work such as the outbox).
<!--#else-->
- `UserId` is the login's ID: the standard name identifier claim, which the token's subject (`sub`) fills. It is `null` without a login, and outside a request (background work such as the outbox).
<!--#endif-->
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

<!--#if (entra)-->
## Microsoft Entra ID

The API validates the access tokens Entra ID issues for your app registration (JWT bearer, `Microsoft.AspNetCore.Authentication.JwtBearer`). Entra ID is an Azure service: you need a tenant (the free tier is enough for development). There is nothing to run locally.

### Registering the API

In the Entra admin center (*Identity › Applications › App registrations*), or with `az ad app create`:

1. **Register an application** for the API, single tenant. Its *Application (client) ID* and *Directory (tenant) ID* are the two settings below.
2. **Expose an API**: set the Application ID URI (`api://<client-id>` by default) and add a scope named `access_as_user`.
3. **App roles**: add a role with the value `admin` (allowed member types: users/groups), then assign it to users under *Enterprise applications › your app › Users and groups*. Assigned roles arrive in the token's `roles` claim and become role claims.
4. For a client that signs users in (a browser app, an API reference page), register it, add the `access_as_user` scope under its *API permissions*, and let it sign users in with the authorization code flow and PKCE.

### Settings

| Setting | Default | |
|---|---|---|
| `Authentication:Entra:TenantId` | – (required) | The directory (tenant) ID |
| `Authentication:Entra:ClientId` | – (required) | The API's application (client) ID |
| `Authentication:Entra:Instance` | `https://login.microsoftonline.com/` | Another cloud's login URL, if needed |
| `Authentication:Entra:Scope` | `access_as_user` | The scope the OpenAPI document asks for |

The API refuses to start without the tenant and client IDs. With the AppHost you don't set them on the API: on the first run the dashboard asks for the parameters `cleanslices-entra-tenant-id` and `cleanslices-entra-client-id`, keeps them in the AppHost's user secrets, and passes them to the API. Without the AppHost, put them in the API's user secrets or environment (`Authentication__Entra__TenantId`, ...).

Tokens are accepted when Entra issued them for the tenant, in either version (v1 tokens come from `https://sts.windows.net/<tenant-id>/`, v2 from `…/<tenant-id>/v2.0`, depending on the registration's *accessTokenAcceptedVersion*), and when their audience is the client ID or `api://<client-id>`.

### Getting a token

The quickest way is the Azure CLI. Allow it once: under *Expose an API › Authorized client applications*, add the Azure CLI's client ID, `04b07795-8ddb-461a-bbee-02f9e1bf7b46`, with the `access_as_user` scope. Then:

```
az login --tenant <tenant-id>
az account get-access-token --scope api://<client-id>/access_as_user --query accessToken -o tsv
```

Paste the token into `@token` at the top of `src/CleanSlices.Api/CleanSlices.Api.http`; it lasts about an hour. The OpenAPI document describes the login (OAuth2, authorization code against your tenant), so API reference pages can sign in with Entra.

### Beyond this option

The option only validates tokens, for one tenant. For calling other APIs on the user's behalf (Microsoft Graph, your own services) or for accepting several tenants, use `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApi`) instead: it adds token acquisition and multi-tenant issuer checks, at the cost of more dependencies.
<!--#endif-->

<!--#if (identity)-->
## ASP.NET Core Identity

The API keeps its own login accounts with ASP.NET Core Identity, in the same database as your data: `AppDbContext` inherits `IdentityDbContext<AppUser>`, so Identity's tables (`AspNetUsers`, `AspNetRoles`, …) come with your migrations. Create one after adding the option:

```
dotnet ef migrations add AddIdentity --project src/CleanSlices.Infrastructure
```

`AppUser` (`Infrastructure/Identity/`) is the login account, not a domain entity; add profile fields to it if you need them. Its ID is Identity's string key, which is what `ICurrentUser.UserId` returns.

### Endpoints

`MapIdentityApi` adds the account endpoints under **`/account`**:

| Endpoint | Login | |
|---|---|---|
| `POST /account/register` | no | `{ email, password }` creates an account |
| `POST /account/login` | no | `{ email, password }` returns `accessToken` (valid for one hour) and `refreshToken` |
| `POST /account/refresh` | no | `{ refreshToken }` returns new tokens |
| `confirmEmail`, `resendConfirmationEmail`, `forgotPassword`, `resetPassword` | no | the email flows |
| `/account/manage/info`, `/account/manage/2fa` | yes | the logged-in user's account |

Send the access token as `Authorization: Bearer <token>`. The tokens are Identity's own format, not JWTs: only this API can read them. The `.http` file registers a user, logs in and sends the token.

Only bearer tokens are set up; `useCookies=true` on login isn't supported. The default password rules apply: at least six characters, with an upper-case letter, a lower-case letter, a digit and a symbol.

### Roles

The `admin` role is seeded (`AppRoles.Admin`, in the model, so it comes with the migration). There are no endpoints that assign roles: give a user a role in your own code, with `UserManager`:

```csharp
var user = await users.FindByEmailAsync(email);
await users.AddToRoleAsync(user!, AppRoles.Admin);
```

Roles are written into the token when the user logs in, so a new role shows up at the next login (or refresh).

### Production notes

- **Emails aren't sent.** Identity's email flows (confirmation, password reset) call `IEmailSender<AppUser>`, which does nothing by default. Register your own implementation to send them; until then, accounts don't need a confirmed email.
- **Share the Data Protection keys.** The tokens are encrypted with ASP.NET Core Data Protection. With several instances, or across restarts in a container, every instance must use the same keys, or tokens from one won't work on another: persist them (`AddDataProtection().PersistKeysTo…`, for example to Redis, a database or Azure Blob Storage).
- Lockout, 2FA and the password rules are configured with `builder.Services.Configure<IdentityOptions>(…)`.
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
<!--#if (entra)-->
The tests don't call Entra ID: the test factory signs its own tokens and the API trusts only that key. Everything else is the real validation (issuer, audience, roles, the object ID as the user ID); only fetching Entra's signing keys isn't tested.
<!--#endif-->
<!--#if (identity)-->
The test users register and log in through `/account/register` and `/account/login`; the admin gets its role through `UserManager`. The test hosts share their Data Protection keys, so a token works on every host a test starts.
<!--#endif-->
