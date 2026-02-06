# Replace ChatContext with ICommandContext in Commands files
# Since VampireCommandFramework may have changed API

# Set root folder of your project
$root = "c:\Users\coyot.RWE\OneDrive\Documents\Vauto (4)\VProgress\All projects"

# Get all C# files in Commands folder
$files = Get-ChildItem -Path "$root\Commands" -Recurse -Include *.cs

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw

    $changed = $false

    # Replace ChatContext with ICommandContext
    $newContent = $content -replace 'ChatContext', 'ICommandContext'

    if ($newContent -ne $content) {
        $changed = $true
        $newContent | Set-Content $file.FullName -Encoding UTF8
    }

    if ($changed) {
        Write-Host "Updated ChatContext to ICommandContext in file: $($file.FullName)" -ForegroundColor Green
    }
}

Write-Host "`nChatContext replacement complete!"
