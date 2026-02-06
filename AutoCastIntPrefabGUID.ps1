# AutoCastPrefabGUID.ps1
# Adds (int) casts to all PrefabGUID constructors in the project

# Set root folder of your project
$root = "c:\Users\coyot.RWE\OneDrive\Documents\Vauto (4)\VProgress\All projects"

# Get all C# files
$files = Get-ChildItem -Path $root -Recurse -Include *.cs

foreach ($file in $files) {
    $content = Get-Content $file.FullName

    $changed = $false

    # Regex to find new PrefabGUID(...) without (int) cast
    $pattern = 'new\s+PrefabGUID\s*\(\s*(?!\(int\))([^\)]+)\s*\)'

    $newContent = $content | ForEach-Object {
        $_ -replace $pattern, 'new PrefabGUID((int)$1)'
    }

    # Check if file changed
    if ($newContent -ne $content) {
        $changed = $true
        $newContent | Set-Content $file.FullName
    }

    if ($changed) {
        Write-Host "Updated PrefabGUID casts in file: $($file.FullName)"
    }
}

Write-Host "`nPrefabGUID casting automation complete!"
