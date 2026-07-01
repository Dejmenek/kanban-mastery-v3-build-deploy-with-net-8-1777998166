# Title
Integration test data setup strategy

# Date
2026-07-01

## Context
Integration tests often need data to exist in the database before the assertion can be made.
For example, testing the login endpoint requires that a user account already exists.
We need a consistent strategy for how that precondition data is created.

## Considered Options

### Suite-level seed data
Seed a fixed dataset once before the entire test suite runs using a collection fixture with `IAsyncLifetime`.
All tests then read from that shared state.

This approach is fast because setup runs only once, but it introduces shared mutable state.
If any test modifies or deletes a seeded row, it can silently break unrelated tests.
Adding test-specific variants also requires polluting the shared seed, making it grow over time and harder to understand.

### Unique database per test class with direct EF Core seeding
Each test class gets its own isolated in-memory database (unique GUID name) via `IClassFixture`.
Seeding is done directly through `ApplicationDbContext` in a shared setup step.

Classes are isolated from each other, and the factory is inexpensive because it is created once per class.
However, tests within the same class still share state, so tests that mutate data can affect their siblings within the class.

### Shared factory per test class with database cleanup between tests
The `WebApplicationFactory` is shared across a test class via `IClassFixture`, so the in-process server starts once per class.
An abstract `IntegrationTestBase` class receives the factory via its constructor and implements `IAsyncLifetime`: `InitializeAsync` resets the database by calling `EnsureDeletedAsync` followed by `EnsureCreatedAsync` before each test runs, and `DisposeAsync` handles cleanup.
The base class exposes a `protected Factory` property; test arrange steps resolve services (e.g. `ApplicationDbContext`) directly from `Factory.Services.CreateScope()`, keeping scope lifetime explicit and under the test's control.
The arrange step then seeds exactly what that test needs and the test asserts through HTTP.

This combines the efficiency of a per-class factory (one server startup per class) with test-level isolation (clean database state at the start of every test).

## Decision
We will use the shared factory per test class with database cleanup between tests.

Every test starts with a clean database, and each test is responsible for seeding only the data it needs to run.
