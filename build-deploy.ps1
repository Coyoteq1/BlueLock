param(
    [string]$Configuration = "Release"
)

$Root = $PSScriptRoot
$Solution = Join-Path $Root "AllProjects.sln"
$ServerPluginsDir = "C:\Program Files (x86)\Steam\VRising_Server\BepInEx\plugins\DedicatedServerLauncher\VRisingServer\BepInEx\plugins"
$ServerConfigDir = "C:\Program Files (x86)\Steam\VRising_Server\BepInEx\config"

Write-Host "Building solution '$Solution' ($Configuration)..."
dotnet build $Solution -c $Configuration

if (-not (Test-Path $ServerPluginsDir)) {
    Write-Host "Creating plugins directory: $ServerPluginsDir"
    New-Item -ItemType Directory -Path $ServerPluginsDir -Force | Out-Null
}

$Projects = @(
    "VAutomationCore",
    "Vlifecycle",
    "VAutoTraps",
    "VAutoannounce",
    "VAutoZone"
)

foreach ($project in $Projects) {
    $ProjectDir = Join-Path $Root $project
    $OutputDir = Join-Path $ProjectDir "bin\$Configuration\net6.0"
    $DllPath = Join-Path $OutputDir "$project.dll"

    if (Test-Path $DllPath) {
        Write-Host "Copying $project -> $ServerPluginsDir"
        Copy-Item $DllPath -Destination $ServerPluginsDir -Force
    }
    else {
        Write-Warning "Missing build output for $project; expected $DllPath"
    }

    $ConfigDirs = @("config", "Configuration", "Json", "Models")
    foreach ($configDirName in $ConfigDirs) {
        $SourceDir = Join-Path $ProjectDir $configDirName
        if (-not (Test-Path $SourceDir)) {
            continue
        }

        Write-Host "Copying configs from $SourceDir -> $ServerConfigDir"
        Get-ChildItem -Path $SourceDir -Recurse -File | ForEach-Object {
            $Relative = $_.FullName.Substring($SourceDir.Length).TrimStart('\','/')
            $Target = Join-Path $ServerConfigDir $Relative
            $TargetDir = Split-Path $Target -Parent
            if (-not (Test-Path $TargetDir)) {
                New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
            }
            Copy-Item $_.FullName -Destination $Target -Force
        }
    }
}

Write-Host "Deploy complete."
