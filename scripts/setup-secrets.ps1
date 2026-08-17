<#
.SYNOPSIS
	Konfigurerar lokala User Secrets för Hyggligt (SeedAdmin) på den här datorn.

.DESCRIPTION
	User Secrets lagras utanför git (i din användarprofil) och måste därför sättas
	upp en gång per dator. Det här scriptet frågar efter värdena interaktivt så att
	inga hemligheter behöver checkas in i repot.

	Kör från repots rot:
		./scripts/setup-secrets.ps1

.NOTES
	Kräver .NET SDK (dotnet CLI). Projektet har redan en UserSecretsId i csproj.
#>

[CmdletBinding()]
param(
	[string]$Email,
	[string]$DisplayName = "Super Admin"
)

$ErrorActionPreference = "Stop"

# Sökväg till projektet relativt scriptets plats (fungerar oavsett var man står).
# Byggs stegvis så det fungerar både i Windows PowerShell 5.1 och PowerShell 7+.
$projekt = Join-Path $PSScriptRoot ".."
$projekt = Join-Path $projekt "PlantApp"
$projekt = Join-Path $projekt "PlantApp.csproj"
$projekt = (Resolve-Path $projekt).Path

if (-not $Email) {
	$Email = Read-Host "Super-admin e-post"
}

# Läs lösenordet maskerat så det inte hamnar i konsolhistoriken.
$sakerLosen = Read-Host "Super-admin lösenord" -AsSecureString
$losen = [System.Net.NetworkCredential]::new("", $sakerLosen).Password

if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($losen)) {
	Write-Error "E-post och lösenord måste anges."
	exit 1
}

Write-Host "Sätter User Secrets för $projekt ..." -ForegroundColor Cyan

dotnet user-secrets set "SeedAdmin:Email"       $Email       --project $projekt | Out-Null
dotnet user-secrets set "SeedAdmin:DisplayName" $DisplayName --project $projekt | Out-Null
dotnet user-secrets set "SeedAdmin:Password"    $losen       --project $projekt | Out-Null

Write-Host "Klart. Starta appen så seedas super-admin i den lokala databasen." -ForegroundColor Green
