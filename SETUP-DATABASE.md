# Local MySQL Setup (Team Guide)

Every team member runs their own local MySQL Server. The app code and EF Core
migrations are shared via git, but each person's password/connection string
stays local (via .NET User Secrets) so nobody commits credentials and nobody
needs to share one password.

## 1. Install MySQL Server (one-time, each machine)

Windows (winget):
```
winget install Oracle.MySQL
```
Or download the installer manually from https://dev.mysql.com/downloads/installer/
(choose "MySQL Installer for Windows").

During setup:
- Keep the default port `3306`.
- Choose **"Use Strong Password Encryption"** (default) — Pomelo/MySqlConnector supports it.
- Set a **root password** and remember it — this is local to your machine only,
  it does not need to match your teammates' passwords.
- Optionally install **MySQL Workbench** in the same wizard for a GUI to browse the DB.

## 2. Confirm the server is running

```
sc query MySQL80
```
Status should show `RUNNING`. (Service name may be `MySQL80` or `MySQL` depending on version.)

## 3. Pull the latest code

The project already has:
- `Pomelo.EntityFrameworkCore.MySql` + EF Core packages wired in the `.csproj`
- `Data/ApplicationDbContext.cs`
- `Program.cs` configured to read `ConnectionStrings:DefaultConnection` and connect via MySQL

You do **not** need to touch these — just pull.

## 4. Set your own connection string (per machine, never committed)

From the project folder (the one with the `.csproj`):
```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "server=localhost;port=3306;database=ddac_scholarship_db;user=root;password=YOUR_OWN_PASSWORD"
```
Replace `YOUR_OWN_PASSWORD` with the root password you set in step 1. This is stored
outside the repo (in your user profile), so it's safe to run even though the project
is version-controlled — it will never show up in `git status`.

The database `ddac_scholarship_db` doesn't need to exist yet — EF Core migrations
create it automatically in step 6.

## 5. Add your models + DbSets

As you build features, add entity classes (e.g. `Models/User.cs`, `Models/Scholarship.cs`)
and register them in `Data/ApplicationDbContext.cs`:
```csharp
public DbSet<User> Users => Set<User>();
```

## 6. Create/update the schema with EF Core Migrations

Whoever changes the models runs:
```
dotnet ef migrations add <DescriptiveName>
dotnet ef database update
```
Commit the generated `Migrations/` folder to git. Everyone else just runs:
```
dotnet ef database update
```
after pulling, to apply the same migrations to their own local database —
this keeps everyone's schema identical without sharing a server.

If `dotnet ef` isn't recognized, install the CLI tool once:
```
dotnet tool install --global dotnet-ef
```

## 7. Run the app

```
dotnet run
```
or press F5 in Visual Studio.

## Notes for later (deployment)

When you deploy to AWS, you'll point `ConnectionStrings:DefaultConnection` at an
Amazon RDS MySQL endpoint instead of `localhost` — same code, no changes needed,
just a different connection string set as an environment variable / Elastic Beanstalk
configuration setting (never committed either).
