param(     do {
    
} until (
    <# Condition that stops the loop if it returns true #>
)
    [string]$ServerPath = "C:\Program Files (x86)\Steam\VRising_Server"
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

$Root = $PSScriptRoot
$Solution = Join-Path $Root "AllProjects.sln"
$ServerPluginsDir = Join-Path $ServerPath "BepInEx\plugins"
$ServerConfigDir = Join-Path $ServerPath "BepInEx\config"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "V Rising Mods - Build & Deploy Script" -ForegroundColor Cyan
Write-Host "========================================" -InformationAction Continue
Write-Host ""

# Verify .NET SDK
Write-Host "Checking .NET SDK..." -InformationAction Continue
$dotnetVersion = dotnet --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error ".NET SDK not found. Please install .NET 6.0 SDK."
    exit 1
}
Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Green -InformationAction Continue

# Build solution
Write-Host ""
Write-Host "Building solution..." -InformationAction Continue
$buildResult = dotnet build $Solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}
Write-Host "Build succeeded!" -ForegroundColor Green -InformationAction Continue

# Verify server directories
Write-Host ""
Write-Host "Verifying server directories..." -InformationAction Continue
if (-not (Test-Path $ServerPluginsDir)) {
    Write-Host "Creating plugins directory: $ServerPluginsDir" -InformationAction Continue
    New-Item -ItemType Directory -Path $ServerPluginsDir -Force | Out-Null
}
if (-not (Test-Path $ServerConfigDir)) {
    Write-Host "Creating config directory: $ServerConfigDir" -InformationAction Continue
    New-Item -ItemType Directory -Path $ServerConfigDir -Force | Out-Null
}

# Project deployment order (dependency order)
$Projects = @(
    @{
        Name = "VAutomationCore"
        Description = "Core library with EntitySpawner"
        ConfigDirs = @("Configuration", "Services")
    },
    @{
        Name = "Vlifecycle"
        Description = "Player lifecycle management"
        ConfigDirs = @("Configuration")
    },
    @{
        Name = "VAutoZone"
        Description = "Zone management with glow borders"
        ConfigDirs = @("config", "Core", "Services", "Models")
    },
    @{
        Name = "VAutoannounce"
        Description = "Announcement system"
        ConfigDirs = @("Commands", "Core", "Services")
    },
    @{
        Name = "VAutoTraps"
        Description = "Trap system"
        ConfigDirs = @("Commands", "Configuration", "Core", "Data", "Services")
    }
)

$totalDeployed = 0
$totalFailed = 0

foreach ($project in $Projects) {
    $projectName = $project.Name
    Write-Host ""
    Write-Host "----------------------------------------" -ForegroundColor Gray
    Write-Host "Deploying: $projectName" -ForegroundColor Yellow
    Write-Host "Description: $($project.Description)" -InformationAction Continue

    $ProjectDir = Join-Path $Root $projectName
    $OutputDir = Join-Path $ProjectDir "bin\$Configuration\net6.0"
    $DllPath = Join-Path $OutputDir "$projectName.dll"

    # Copy DLL
    if (Test-Path $DllPath) {
        $DestDll = Join-Path $ServerPluginsDir "$projectName.dll"
        Copy-Item $DllPath -Destination $DestDll -Force
        Write-Host "  [OK] DLL copied: $projectName.dll" -ForegroundColor Green
        $totalDeployed++
    }
    else {
        Write-Host "  [FAIL] Missing DLL: $DllPath" -ForegroundColor Red
        $totalFailed++
        continue
    }

    # Copy configuration and resource directories
    foreach ($configDir in $project.ConfigDirs) {
        $SourceDir = Join-Path $ProjectDir $configDir
        if (-not (Test-Path $SourceDir)) {
            continue
        }

        Write-Host "  Copying $configDir..." -InformationAction Continue
        Get-ChildItem -Path $SourceDir -Recurse -File | ForEach-Object {
            $Relative = $_.FullName.Substring($SourceDir.Length).TrimStart('\', '/')
            $Target = Join-Path $ServerConfigDir "$projectName\$Relative"
            $TargetDir = Split-Path $Target -Parent
            if (-not (Test-Path $TargetDir)) {
                New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
            }
            Copy-Item $_.FullName -Destination $Target -Force
        }
    }

    # Copy JSON config files
    $JsonFiles = Get-ChildItem -Path $ProjectDir -Filter "*.json" -File
    foreach ($jsonFile in $JsonFiles) {
        $DestJson = Join-Path $ServerConfigDir $jsonFile.Name
        Copy-Item $jsonFile.FullName -Destination $DestJson -Force
        Write-Host "  [OK] Config copied: $($jsonFile.Name)" -InformationAction Continue
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Summary" -ForegroundColor Cyan
Write-Host "========================================" -InformationAction Continue
Write-Host "Total deployed: $totalDeployed" -InformationAction Continue
Write-Host "Total failed: $totalFailed" -InformationAction Continue
Write-Host "Plugins dir: $ServerPluginsDir" -InformationAction Continue
Write-Host "Config dir: $ServerConfigDir" -InformationAction Continue

if ($totalFailed -eq 0) {
    Write-Host ""
    Write-Host "All mods deployed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -InformationAction Continue
    Write-Host "1. Restart your V Rising server" -InformationAction Continue
    Write-Host "2. Check server logs for mod loading messages" -InformationAction Continue
    Write-Host "3. Test EntitySpawner with .lifecycle or .arena commands" -InformationAction Continue
}
else {
    Write-Host ""
    Write-Warning "Some deployments failed. Check the output above."
    exit 1
}
