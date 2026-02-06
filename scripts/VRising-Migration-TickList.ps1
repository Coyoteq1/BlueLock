# V Rising 1.1 Migration Tick-List Script
# Interactive checklist with progress tracking and state persistence

param(
    [string]$StateFile = "$PSScriptRoot\..\migration_state.json",
    [switch]$Reset
)

# ============================================================================
# Data Structures
# ============================================================================

$script:Tasks = @(
    @{
        Id = 1
        Title = "Add null check in EndGameKitCommandHelper.cs"
        Description = "Ensure KitName parameter has proper null validation"
        Category = "Code Fix"
        Status = $false
    },
    @{
        Id = 2
        Title = "Cache EndGameKitSystem instance"
        Description = "Cache the EndGameKitSystem singleton for performance"
        Category = "Optimization"
        Status = $false
    },
    @{
        Id = 3
        Title = "Validate KitName component in KitRequestSystem.cs"
        Description = "Add component validation before processing kit requests"
        Category = "Code Fix"
        Status = $false
    },
    @{
        Id = 4
        Title = "Cache reflection for zone check in KitCommands.cs"
        Description = "Cache reflection methods to avoid repeated MethodInfo lookups"
        Category = "Optimization"
        Status = $false
    },
    @{
        Id = 5
        Title = "Remove redundant VRCore.Initialize() calls"
        Description = "Audit and remove duplicate VRCore initialization calls"
        Category = "Cleanup"
        Status = $false
    },
    @{
        Id = 6
        Title = "Audit KitRecordsService references"
        Description = "Verify all KitRecordsService dependencies are properly injected"
        Category = "Code Audit"
        Status = $false
    },
    @{
        Id = 7
        Title = "Test KitRequestSystem (ECS)"
        Description = "Run unit tests for KitRequestSystem component"
        Category = "Testing"
        Status = $false
    },
    @{
        Id = 8
        Title = "Test VBloodRepairRefreshSystem (ECS)"
        Description = "Run integration tests for VBloodRepairRefreshSystem"
        Category = "Testing"
        Status = $false
    },
    @{
        Id = 9
        Title = "Test ArenaAutoEnterSystem (ECS)"
        Description = "Validate ArenaAutoEnterSystem functionality"
        Category = "Testing"
        Status = $false
    },
    @{
        Id = 10
        Title = "Verify PrefabGUID migration"
        Description = "Confirm PrefabGuidConverter changes are applied correctly"
        Category = "Verification"
        Status = $false
    },
    @{
        Id = 11
        Title = "Run full multiplayer tests"
        Description = "Execute comprehensive multiplayer scenario tests"
        Category = "Testing"
        Status = $false
    },
    @{
        Id = 12
        Title = "Document migration changes"
        Description = "Update migration documentation with all changes made"
        Category = "Documentation"
        Status = $false
    }
)

# ============================================================================
# State Management
# ============================================================================

function Save-State {
    $state = @{
        Version = "1.0"
        LastUpdated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        Tasks = $script:Tasks | ForEach-Object {
            @{
                Id = $_.Id
                Status = $_.Status
            }
        }
    }
    $state | ConvertTo-Json -Depth 10 | Out-File $StateFile -Encoding UTF8
    Write-Host "  [+] State saved to: $StateFile" -ForegroundColor Green
}

function Load-State {
    if (Test-Path $StateFile) {
        try {
            $savedState = Get-Content $StateFile -Raw | ConvertFrom-Json
            if ($savedState -and $savedState.Tasks) {
                foreach ($savedTask in $savedState.Tasks) {
                    $task = $script:Tasks | Where-Object { $_.Id -eq $savedTask.Id }
                    if ($task) {
                        $task.Status = [bool]$savedTask.Status
                    }
                }
                Write-Host "  [+] State loaded from: $StateFile" -ForegroundColor Cyan
                return $true
            }
        } catch {
            Write-Host "  [!] Failed to load state: $_" -ForegroundColor Yellow
        }
    }
    return $false
}

function Reset-State {
    foreach ($task in $script:Tasks) {
        $task.Status = $false
    }
    if (Test-Path $StateFile) {
        Remove-Item $StateFile -Force
    }
    Write-Host "  [+] All tasks reset" -ForegroundColor Yellow
}

# ============================================================================
# Progress Tracking
# ============================================================================

function Get-Progress {
    $completed = ($script:Tasks | Where-Object { $_.Status }).Count
    $total = $script:Tasks.Count
    $percent = [math]::Round(($completed / $total) * 100, 1)
    return @{
        Completed = $completed
        Total = $total
        Percent = $percent
    }
}

function Show-ProgressBar {
    param([int]$Percent, [int]$Width = 30)
    $filled = [math]::Round(($Width * $percent) / 100)
    $empty = $Width - $filled
    $bar = ("#" * $filled) + ("-" * $empty)
    return "[$bar] $Percent%"
}

# ============================================================================
# UI Functions
# ============================================================================

function Write-Title {
    param([string]$Text)
    $width = 60
    $padding = ($width - $Text.Length - 2) / 2
    $padLeft = [math]::Floor($padding)
    $padRight = [math]::Ceiling($padding)
    
    Write-Host ("=" * $width) -ForegroundColor Magenta
    Write-Host (" " * $padLeft + "[ $Text ]" + " " * $padRight) -ForegroundColor Magenta
    Write-Host ("=" * $width) -ForegroundColor Magenta
}

function Show-Menu {
    param(
        [string]$Title,
        [array]$Items,
        [string]$Prompt = "Select an option"
    )
    
    Write-Host "`n  $Title" -ForegroundColor Yellow
    Write-Host "  " + ("-" * ($Title.Length + 2))
    
    for ($i = 0; $i -lt $Items.Count; $i++) {
        $item = $Items[$i]
        $prefix = $null
        $color = "White"
        
        if ($item.Keys -contains "Key") {
            $prefix = "[$($item.Key)] "
        }
        if ($item.Keys -contains "Color") {
            $color = $item.Color
        }
        
        Write-Host "  $prefix$($item.Text)" -ForegroundColor $color
    }
    
    Write-Host ""
}

function Toggle-Task {
    param([int]$TaskId)
    $task = $script:Tasks | Where-Object { $_.Id -eq $TaskId }
    if ($task) {
        $task.Status = -not $task.Status
        $statusText = if ($task.Status) { "COMPLETED" } else { "PENDING  " }
        $color = if ($task.Status) { "Green" } else { "Gray" }
        Write-Host "  Task $($TaskId): $($task.Title)" -ForegroundColor $color
        Save-State
    }
}

function Show-TaskDetails {
    param([int]$TaskId)
    $task = $script:Tasks | Where-Object { $_.Id -eq $TaskId }
    if ($task) {
        Write-Host "`n  ╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
        Write-Host "  ║ Task #$($TaskId) Details                      ║" -ForegroundColor Cyan
        Write-Host "  ╠══════════════════════════════════════════════╣" -ForegroundColor Cyan
        Write-Host "  ║ Title: $($task.Title)" -ForegroundColor White
        $titlePadding = 48 - $task.Title.Length
        if ($titlePadding -gt 0) { Write-Host (" " * $titlePadding) -NoNewline }
        Write-Host "║" -ForegroundColor Cyan
        Write-Host "  ╠══════════════════════════════════════════════╣" -ForegroundColor Cyan
        Write-Host "  ║ Category: $($task.Category)" -ForegroundColor White
        $catPadding = 42 - $task.Category.Length
        if ($catPadding -gt 0) { Write-Host (" " * $catPadding) -NoNewline }
        Write-Host "║" -ForegroundColor Cyan
        Write-Host "  ╠══════════════════════════════════════════════╣" -ForegroundColor Cyan
        Write-Host "  ║ Status:   [$($task.Status.ToString().ToUpper())]" -ForegroundColor $(if ($task.Status) { "Green" } else { "Yellow" })
        $statusPadding = 38 - $task.Status.ToString().Length
        if ($statusPadding -gt 0) { Write-Host (" " * $statusPadding) -NoNewline }
        Write-Host "║" -ForegroundColor Cyan
        Write-Host "  ╠══════════════════════════════════════════════╣" -ForegroundColor Cyan
        Write-Host "  ║ Description:                                   ║" -ForegroundColor Cyan
        $words = $task.Description -split ' '
        $line = ""
        $lineNum = 0
        $lines = @()
        foreach ($word in $words) {
            if (($line + " " + $word).Length -le 46) {
                $line = ($line + " " + $word).Trim()
            } else {
                $lines += $line
                $line = $word
            }
        }
        $lines += $line
        foreach ($l in $lines) {
            Write-Host "  ║   $l" -ForegroundColor White
            $lPadding = 46 - $l.Length
            if ($lPadding -gt 0) { Write-Host (" " * $lPadding) -NoNewline }
            Write-Host "║" -ForegroundColor Cyan
        }
        Write-Host "  ╚══════════════════════════════════════════════╝" -ForegroundColor Cyan
        Write-Host ""
    }
}

function Show-AllTasks {
    $progress = Get-Progress
    Write-Host "`n  Progress: $(Show-ProgressBar -Percent $progress.Percent)" -ForegroundColor $(if ($progress.Percent -eq 100) { "Green" } else { "Yellow" })
    Write-Host "  Completed: $($progress.Completed)/$($progress.Total) tasks" -ForegroundColor Gray
    Write-Host ""
    
    # Group by category
    $categories = $script:Tasks | Select-Object -ExpandProperty Category -Unique
    
    foreach ($category in $categories) {
        $catTasks = $script:Tasks | Where-Object { $_.Category -eq $category }
        Write-Host "  ┌─[ $category ]" -ForegroundColor Magenta -NoNewline
        Write-Host ("─" * (48 - $category.Length)) -ForegroundColor Magenta -NoNewline
        Write-Host "┐" -ForegroundColor Magenta
        
        foreach ($task in $catTasks) {
            $statusChar = if ($task.Status) { "✓" } else { "○" }
            $color = if ($task.Status) { "Green" } else { "Gray" }
            $numStr = $task.Id.ToString().PadLeft(2)
            $titleTrunc = if ($task.Title.Length -gt 50) { $task.Title.Substring(0, 47) + "..." } else { $task.Title }
            Write-Host "  │ $numStr [$statusChar] $titleTrunc" -ForegroundColor $color
        }
        Write-Host "  └" -ForegroundColor Magenta -NoNewline
        Write-Host ("─" * 56) -ForegroundColor Magenta -NoNewline
        Write-Host "┘" -ForegroundColor Magenta
    }
}

function Show-QuickStats {
    $progress = Get-Progress
    $byCategory = @{}
    foreach ($task in $script:Tasks) {
        if (-not $byCategory.ContainsKey($task.Category)) {
            $byCategory[$task.Category] = @{ Completed = 0; Total = 0 }
        }
        $byCategory[$task.Category].Total++
        if ($task.Status) {
            $byCategory[$task.Category].Completed++
        }
    }
    
    Write-Host "`n  Quick Stats by Category:" -ForegroundColor Yellow
    foreach ($cat in $byCategory.Keys) {
        $data = $byCategory[$cat]
        $percent = [math]::Round(($data.Completed / $data.Total) * 100)
        $catColor = if ($percent -eq 100) { "Green" } elseif ($percent -ge 50) { "Yellow" } else { "Red" }
        $bar = Show-ProgressBar -Percent $percent -Width 15
        Write-Host "    $cat`: $bar $($data.Completed)/$($data.Total)" -ForegroundColor $catColor
    }
}

# ============================================================================
# Main Interactive Loop
# ============================================================================

function Start-InteractiveMenu {
    Write-Title "V Rising 1.1 Migration"
    
    $loaded = Load-State
    if (-not $loaded) {
        Write-Host "  [!] Starting fresh (no saved state found)" -ForegroundColor Yellow
    }
    
    if ($Reset) {
        Reset-State
    }
    
    $running = $true
    $lastInput = $null
    
    while ($running) {
        $progress = Get-Progress
        Write-Host "`n" -NoNewline
        Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor $(if ($progress.Percent -eq 100) { "Green" } else { "DarkGray" })
        Write-Host "  PROGRESS: $(Show-ProgressBar -Percent $progress.Percent -Width 40)" -ForegroundColor $(if ($progress.Percent -eq 100) { "Green" } else { "Yellow" })
        Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor $(if ($progress.Percent -eq 100) { "Green" } else { "DarkGray" })
        
        $menuItems = @(
            @{ Key = "1-9"; Text = "Toggle task 1-9"; Color = "Cyan" },
            @{ Key = "0"; Text = "Toggle task 10"; Color = "Cyan" },
            @{ Key = "A"; Text = "Toggle all tasks"; Color = "Cyan" },
            @{ Key = "D"; Text = "View task details (format: D <id>)"; Color = "Cyan" },
            @{ Key = "L"; Text = "List all tasks with status"; Color = "Cyan" },
            @{ Key = "S"; Text = "Show statistics by category"; Color = "Cyan" },
            @{ Key = "R"; Text = "Reset all tasks"; Color = "Red" },
            @{ Key = "Q"; Text = "Quit and save"; Color = "Green" }
        )
        
        Show-Menu -Title "COMMANDS" -Items $menuItems -Prompt "Enter command"
        
        $progress = Get-Progress
        if ($progress.Percent -eq 100) {
            Write-Host "`n  🎉 All tasks completed! 🎉" -ForegroundColor Green
        }
        
        Write-Host -NoNewline "`n  > "
        $input = Read-Host
        Write-Host ""
        
        try {
            switch -Regex ($input.Trim().ToUpper()) {
                "^([1-9]|10)$" {
                    Toggle-Task -TaskId ([int]$_)
                }
                "^D\s+(\d+)$" {
                    Show-TaskDetails -TaskId ([int]$matches[1])
                }
                "^A$" {
                    $allDone = ($script:Tasks | Where-Object { -not $_.Status }).Count -eq 0
                    if ($allDone) {
                        Reset-State
                    } else {
                        foreach ($task in $script:Tasks) {
                            $task.Status = $true
                        }
                        Save-State
                    }
                    Write-Host "  [+] All tasks marked complete" -ForegroundColor Green
                }
                "^L$" {
                    Show-AllTasks
                }
                "^S$" {
                    Show-QuickStats
                }
                "^R$" {
                    $confirmation = Read-Host "  Are you sure you want to reset all tasks? (y/N)"
                    if ($confirmation -match "^[Yy]$") {
                        Reset-State
                    }
                }
                "^Q$" {
                    Save-State
                    Write-Host "  [+] Goodbye! State has been saved." -ForegroundColor Green
                    $running = $false
                }
                "^$" {
                    # Empty input, re-show menu
                }
                default {
                    Write-Host "  [!] Invalid command. Use 1-9, 0, A, D <id>, L, S, R, or Q" -ForegroundColor Red
                }
            }
        } catch {
            Write-Host "  [!] Error processing input: $_" -ForegroundColor Red
        }
    }
}

# ============================================================================
# Non-Interactive Functions (for scripting)
# ============================================================================

function Get-TaskStatus {
    param([int]$TaskId)
    $task = $script:Tasks | Where-Object { $_.Id -eq $TaskId }
    return $task.Status
}

function Set-TaskStatus {
    param(
        [int]$TaskId,
        [bool]$Status
    )
    $task = $script:Tasks | Where-Object { $_.Id -eq $TaskId }
    if ($task) {
        $task.Status = $Status
        Save-State
    }
}

function Get-AllTaskStatus {
    return $script:Tasks | Select-Object @{N='Id';E={$_.Id}}, 
                                           @{N='Title';E={$_.Title}}, 
                                           @{N='Status';E={$_.Status}},
                                           @{N='Category';E={$_.Category}}
}

function Get-MigrationProgress {
    return Get-Progress
}

# ============================================================================
# Entry Point
# ============================================================================

# Check if script is being run directly (not dot-sourced)
$isDirectRun = $MyInvocation.Line -eq $MyInvocation.MyCommand.Name

if ($isDirectRun) {
    Start-InteractiveMenu
} else {
    Write-Host "  [i] VRising-Migration-TickList loaded. Run 'Start-InteractiveMenu' to begin." -ForegroundColor Cyan
}

# Export functions for module use
# Export functions for module use (only when loaded as module)
if ($MyInvocation.MyCommand.ScriptBlock.Module) {
    Export-ModuleMember -Function Start-InteractiveMenu, Get-TaskStatus, Set-TaskStatus, 
                               Get-AllTaskStatus, Get-MigrationProgress, Save-State, 
                               Load-State, Reset-State
}