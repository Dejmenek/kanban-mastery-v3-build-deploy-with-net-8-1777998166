# Title
Integration test authentication strategy

# Date
2026-07-02

## Context
Protected endpoints, such as `GET /api/users/me`, require an authenticated `ClaimsPrincipal` to be present on the request before their logic runs.
Integration tests targeting these endpoints need a way to present that authenticated identity to the test host.

## Considered Options

### Real register + login roundtrip
Seed a user via `UserManager`, then POST to `/login` and attach the real `AccessToken` returned in the response to subsequent requests.

This exercises the full, real authentication pipeline end to end, so a protected-endpoint test also implicitly verifies that login still works, and it requires no additional test infrastructure.
However, every protected-endpoint test now depends on the login endpoint succeeding, coupling unrelated test failures together, and each test pays the cost of an extra HTTP round trip and password hashing just to obtain a token.

### Custom TestAuthHandler scheme
Implement `TestAuthHandler`, a test-only `AuthenticationHandler`, that builds a `ClaimsPrincipal` from a header the test sets directly (`X-Test-UserId`), and register it as the default scheme only inside `IntegrationTestWebAppFactory`.
The user itself is still seeded for real via `UserManager`, the same way `AuthTests.cs` already does, so the endpoint's database lookup succeeds against real data.

This keeps protected-endpoint tests fast and isolated from the login subsystem, which is already covered separately by `AuthTests.cs`, and makes it trivial to test arbitrary user ids without going through password hashing or HTTP round trips.
The trade-off is that these tests no longer exercise real token issuance, and the handler is additional test infrastructure that has to be kept in sync with what the application's authorization policies expect from a `ClaimsPrincipal`.

## Decision
We will use the real register + login roundtrip.

Protected-endpoint tests exercise the real bearer-token pipeline end to end, so `GET /api/users/me` doubles as a smoke test for authentication itself: if login or token validation breaks, this endpoint's tests fail along with it. We accept the coupling to the login endpoint and the extra per-test cost, since `AuthTests.cs` already isolates login-specific failures by name and the endpoint under test is cheap enough that the added round trip is negligible.
