# Digital Scholarship Management System (DDAC Group Project)

ASP.NET Core MVC app with ASP.NET Core Identity for login/roles, EF Core (code-first) against MySQL.
Locally everyone runs their own MySQL; the deployed app points at AWS RDS MySQL.

## Roles

| Role | Person | Identity role name |
|---|---|---|
| Admin | Abdul Rauf | `Admin` |
| Scholarship Officer | Bryan Wong Tze Hern | `Officer` |
| Student/Applicant | Kareshma Kaur | `Student` |
| Reviewer | Muhammad Shamel | `Reviewer` |

Public registration (the `/Identity/Account/Register` page) always creates a **Student** account.
Admin/Officer/Reviewer accounts are provisioned, not self-registered — for now, use the seeded
demo accounts below until the Admin's "manage user accounts" feature exists.

## One-time local setup

1. **Install MySQL locally** (MySQL Community Server + Workbench, or XAMPP). During setup, set the
   `root` password to `DDAC_G4` — that's the team convention baked into
   `appsettings.Development.json` (`localhost:3306`, user `root`). If your local root password is
   genuinely different, either change it to match, or edit the connection string in that file
   locally and just don't commit that specific change (`dotnet user-secrets` is the "proper" way to
   do this, but it didn't reliably load under Visual Studio's debugger for us — editing the file
   locally and not committing the change is the more dependable fallback).

2. **Create the database and apply the schema:**

   ```bash
   cd "Digital Scholarship Management System DDAC"
   dotnet tool install -g dotnet-ef --version 9.0.0   # once per machine
   dotnet ef database update
   ```

   This creates the `ddac_scholarship` database and every table from the current migration.
   Whenever someone adds a new migration (see below), pull it and re-run `dotnet ef database update`.

3. **Run the app:**

   ```bash
   dotnet run
   ```

   On first run, `DbSeeder` (`Data/DbSeeder.cs`) creates the 4 roles and one demo login per role,
   all with password `Passw0rd!`:

   - `admin@ddac.edu`
   - `officer@ddac.edu`
   - `student@ddac.edu`
   - `reviewer@ddac.edu`

## Project structure (so we don't collide)

- `Areas/Identity/` — scaffolded login/register/manage pages (ASP.NET Core Identity). Shared;
  changes here affect everyone, so flag it in the group chat before editing.
- `Areas/Identity/Data/ApplicationDbContext.cs` — the single EF Core `DbContext`. All `DbSet`s
  (including our domain tables) live here.
- `Areas/Identity/Data/ApplicationUser.cs` — the login user record (email, password, `FullName`).
- `Models/` — domain entity classes: `Scholarship`, `Application`, `Document`, `Review`,
  `StudentProfile`, `Notification`.
- `Controllers/<Role>Controller.cs` + `Views/<Role>/` — one controller/view-folder per role,
  gated with `[Authorize(Roles = "...")]`. This is where each of us builds our own features
  without touching each other's files.
- `Migrations/` — EF Core migration history. **Only one person adds a migration at a time** —
  say so in the group chat, push it, then everyone else pulls and runs `dotnet ef database update`.

## Adding a new migration (when you change/add an entity)

```bash
cd "Digital Scholarship Management System DDAC"
dotnet ef migrations add <DescriptiveName>
dotnet ef database update
```

Commit both the new files under `Migrations/` and your entity/DbContext changes together.

## Deploying to AWS

- Database: AWS RDS (MySQL).
- Compute: Elastic Beanstalk or EC2 (pick one).
- The production connection string is **not** committed — `appsettings.json`'s
  `ConnectionStrings:ApplicationDbContextConnection` is left empty on purpose. Set it via the
  environment variable `ConnectionStrings__ApplicationDbContextConnection` on the AWS compute
  service (Elastic Beanstalk environment properties / EC2 environment config) pointing at the
  RDS endpoint.
- Before the demo, run `dotnet ef database update` once against the real RDS instance (or apply
  the same migrations) so the deployed schema matches.
