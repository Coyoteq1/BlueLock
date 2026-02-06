param(
  [Parameter(Mandatory = $false)]
  [string]$Root = "."
)

$ErrorActionPreference = "Stop"

try {
  Add-Type -AssemblyName System.Text.Json | Out-Null
} catch {
  # If this fails, the environment likely lacks System.Text.Json in the GAC.
  # We'll fall back to ConvertFrom-Json / ConvertTo-Json later if needed.
}

function Is-GeneratedPath([string]$path) {
  $p = $path.Replace("\", "/").ToLowerInvariant()
  return ($p -match "/bin/" -or $p -match "/obj/")
}

function Test-CamelCaseKeys([string]$json, [string]$path) {
  # Best-effort: look for property names that start with A-Z
  $matches = [regex]::Matches($json, '"([A-Z][^"]*)"\s*:')
  if ($matches.Count -gt 0) {
    $unique = $matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    Write-Warning ("Non-camelCase keys in {0}: {1}" -f $path, ($unique -join ", "))
  }
}

function Format-JsonFile([string]$path) {
  $raw = Get-Content -Raw -LiteralPath $path

  $hasSystemTextJson = ($null -ne ([Type]::GetType("System.Text.Json.JsonDocument, System.Text.Json", $false)))

  if ($hasSystemTextJson) {
    # RFC 8259 strict parse: no comments, no trailing commas.
    try {
      $doc = [System.Text.Json.JsonDocument]::Parse($raw, [System.Text.Json.JsonDocumentOptions]@{
        AllowTrailingCommas = $false
        CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
      })
    } catch {
      throw ("Invalid JSON (RFC 8259) in {0}: {1}" -f $path, $_.Exception.Message)
    }

    Test-CamelCaseKeys -json $raw -path $path

    $opts = [System.Text.Json.JsonWriterOptions]@{
      Indented = $true
      SkipValidation = $false
    }

    $ms = New-Object System.IO.MemoryStream
    $writer = New-Object System.Text.Json.Utf8JsonWriter($ms, $opts)
    $doc.RootElement.WriteTo($writer)
    $writer.Flush()

    $formatted = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    if (-not $formatted.EndsWith("`n")) { $formatted += "`n" }
    Set-Content -LiteralPath $path -Value $formatted -Encoding UTF8
    return
  }

  # Fallback for Windows PowerShell environments without System.Text.Json.
  # ConvertFrom-Json is strict enough to reject trailing commas and comments.
  try {
    $obj = $raw | ConvertFrom-Json -ErrorAction Stop
  } catch {
    throw ("Invalid JSON in {0}: {1}" -f $path, $_.Exception.Message)
  }

  Test-CamelCaseKeys -json $raw -path $path
  $formatted = ($obj | ConvertTo-Json -Depth 100)
  if (-not $formatted.EndsWith("`n")) { $formatted += "`n" }
  Set-Content -LiteralPath $path -Value $formatted -Encoding UTF8
}

$all = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter *.json
$targets = $all | Where-Object { -not (Is-GeneratedPath $_.FullName) }

foreach ($f in $targets) {
  # Only format "source" configs and manifests; avoid touching lock/assets files.
  $name = $f.Name.ToLowerInvariant()
  $p = $f.FullName.Replace("\", "/").ToLowerInvariant()

  $isConfig =
    ($p -match "/configuration/" -or
     $p -match "/config/" -or
     $name -eq "global.json" -or
     $name -eq "manifest.json" -or
     $name -eq "repo_info.json")

  if (-not $isConfig) { continue }

  Write-Host "Formatting $($f.FullName)"
  Format-JsonFile -path $f.FullName
}

Write-Host "Done."
