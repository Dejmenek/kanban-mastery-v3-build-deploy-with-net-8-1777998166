# Kanban

A full-stack Kanban board application built with **ASP.NET Core 8** (Web API) and **Angular 22**. It lets teams organize work into boards, columns, and cards, invite members, and track ownership — with drag-and-drop reordering and role-based access control baked into the API.

## 🌐 Demo

[Live Demo](https://red-bay-0ce75de03.7.azurestaticapps.net)

You can register your own account directly on the demo.

## 📖 About this Software

This project is a Trello-style Kanban board built to explore production-grade patterns for a .NET + Angular stack.

Each board has an **owner** and a list of **members**. Owners manage the board and its membership; members can create and reorganize columns and cards. Boards contain columns, columns contain cards, and cards can be assigned to a member and moved between columns.

### Features

- **Authentication** — register/login via ASP.NET Core Identity API endpoints.
- **Boards** — create, view, rename, and delete boards you own or belong to.
- **Columns** — add, rename, delete, and reorder columns within a board.
- **Cards** — create, edit, delete, assign to a board member, and drag-and-drop reorder/move between columns.
- **Board membership** — owners can add members and search through the member list.
- **Role-based authorization** — custom `IsBoardOwner` / `IsBoardMember` policies guard every board, column, card, and member endpoint.
- **Resilient data access** — SQL Server with EF Core retry-on-failure and a conflict-aware retry executor for concurrent position updates.

## 🖼️ Screenshots

<img width="1868" height="894" alt="image" src="https://github.com/user-attachments/assets/9ab66f96-f16c-45d2-bc06-da2afe9d1dad" />
<img width="1868" height="894" alt="image" src="https://github.com/user-attachments/assets/696caa48-96cf-4d86-a595-e7ef1d7a43ab" />


## 🛠️ Tech Stack

- **Backend:** ASP.NET Core 8 (Minimal APIs), Entity Framework Core, ASP.NET Core Identity, SQL Server
- **Frontend:** Angular 22, Angular CDK
- **Testing:** xUnit, Testcontainers (SQL Server) for integration tests, EF Core InMemory for unit tests

## 🚀 Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) 20+ and npm
- [Angular CLI](https://angular.dev/tools/cli) (`npm install -g @angular/cli`)
- [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio, or installable via the SQL Server Express installer) — used by the API in development
- [Docker](https://www.docker.com/) — required to run the backend **integration** tests, which spin up a real SQL Server instance via Testcontainers

### Backend (Kanban.API)

1. Run the API:
   ```bash
   dotnet run --project src/Kanban.API
   ```
2. On startup (in the Development environment) the API automatically creates the LocalDB database, applies EF Core migrations, and seeds it with sample boards/users — no manual migration step needed. Seeded users (`alice@example.com`, `bob@example.com`, `carol@example.com`) all use the password `Passw0rd!`, handy for logging into the local frontend.
3. The API starts at `https://localhost:7234` (and `http://localhost:5059`). Swagger UI is available at `/swagger`.

### Frontend (kanban-web)

1. Install dependencies:
   ```bash
   cd client/kanban-web
   npm install
   ```
2. Start the dev server:
   ```bash
   ng serve --host=127.0.0.1
   ```
3. The app runs at `http://127.0.0.1:4200` and talks to the API at `https://localhost:7234` (see `src/environments/environment.development.ts`). Make sure the backend is running first.

### Running Backend Tests

- **Unit tests** (no external dependencies, uses EF Core InMemory):
  ```bash
  dotnet test tests/Kanban.API.UnitTests
  ```
- **Integration tests** (requires Docker running, spins up a SQL Server container via Testcontainers):
  ```bash
  dotnet test tests/Kanban.API.IntegrationTests
  ```

## 📝 TODO

- **API versioning** — introduce versioned routes (e.g. `/api/v1/...`) so the API can evolve without breaking existing clients.
