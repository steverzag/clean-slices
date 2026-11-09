# API reference

**Scalar** shows an interactive page for browsing and trying the API, built from the OpenAPI document the API serves. It runs in Development only, at **`/scalar`** (`https://localhost:7155/scalar`); the Aspire dashboard links to it from the API resource (*API reference*).

The page lists the endpoints by tag, with their requests and responses, and sends requests from the browser.

<!--#if (keycloak)-->
## Signing in

The OpenAPI document describes the Keycloak login (OAuth2, authorization code with PKCE) and names the client (`x-scalar-client-id`), so the page's *Authorize* button signs in through Keycloak, with the realm's test users (`user` / `user`, `admin` / `admin`). The realm's client already allows the API's addresses as redirect URIs.
<!--#elseif (entra)-->
## Signing in

The OpenAPI document describes the Entra ID login (OAuth2, authorization code with PKCE, the `access_as_user` scope) and names the client (`x-scalar-client-id`: the API's client ID). For the *Authorize* button to work, the app registration must allow the page: under *Authentication*, add a *Single-page application* platform with the page's address as redirect URI (`https://localhost:7155/scalar/`, as the browser shows it).
<!--#elseif (identity)-->
## Signing in

The OpenAPI document describes a bearer token. Log in with `POST /account/login` (on the page itself), copy the `accessToken` from the response, and paste it under *Authorize*.
<!--#endif-->

The page and the OpenAPI document need no login, and neither is mapped outside Development. To keep it in other environments, move `MapScalarApiReference()` out of the `IsDevelopment()` block, and protect it there.
