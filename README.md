# Hyggligt

Blazor-app (.NET 10) för skogsvård och inventering med offlinestöd för fältarbete.

## Kom igång lokalt

1. **Databas** – appen använder SQL Server LocalDB och kör migrationer automatiskt vid start.
   Ingen manuell setup krävs; en tom databas skapas första gången.

2. **Super-admin (User Secrets)** – inloggningsbar administratör seedas från konfiguration.
   Värdena lagras i User Secrets (utanför git) och måste sättas upp **en gång per dator**:

   ```powershell
   ./scripts/setup-secrets.ps1
   ```

   Scriptet frågar efter e-post och lösenord interaktivt. Alternativt manuellt:

   ```powershell
   dotnet user-secrets set "SeedAdmin:Email" "<din-epost>" --project PlantApp/PlantApp.csproj
   dotnet user-secrets set "SeedAdmin:Password" "<ditt-lösenord>" --project PlantApp/PlantApp.csproj
   dotnet user-secrets set "SeedAdmin:DisplayName" "Super Admin" --project PlantApp/PlantApp.csproj
   ```

3. **Kör appen** från Visual Studio eller med `dotnet run --project PlantApp`.
   Vid start seedas roller och (om `SeedAdmin` är konfigurerat) en super-admin.

## Hemligheter och konfiguration

- **Checka aldrig in hemligheter** (lösenord, riktiga e-postadresser) i `appsettings*.json`.
- Lokalt: använd **User Secrets** (se ovan). De är per dator och följer inte med git.
- I en framtida produktionsmiljö: använd **miljövariabler** eller en secret manager.
  Nycklar mappas med dubbla understreck, t.ex. `SeedAdmin__Email` och `SeedAdmin__Password`.
