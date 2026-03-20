Write-Host "Cleaning LAN Game repository..." -ForegroundColor Green

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot

try {
    $projectRoots = Get-ChildItem -Recurse -Filter *.csproj | Select-Object -ExpandProperty DirectoryName -Unique
    $targets = @()

    foreach ($projectRoot in $projectRoots) {
        foreach ($directoryName in @("bin", "obj")) {
            $candidate = Join-Path $projectRoot $directoryName
            if (Test-Path $candidate) {
                $targets += (Resolve-Path $candidate).Path
            }
        }
    }

    $targets += Get-ChildItem -Path $repoRoot -Recurse -Force -Directory |
        Where-Object {
            $_.Name -in @("publish", "TestResults") -and
            $_.FullName -notlike "*\.git\*"
        } |
        Select-Object -ExpandProperty FullName

    foreach ($rootArtifact in @(".vs", "build", "dist", "out", "publish", "target", "tmp")) {
        $candidate = Join-Path $repoRoot $rootArtifact
        if (Test-Path $candidate) {
            $targets += (Resolve-Path $candidate).Path
        }
    }

    $targets = $targets |
        Sort-Object -Unique |
        Sort-Object { $_.Length } -Descending

    if (-not $targets) {
        Write-Host "No generated directories found." -ForegroundColor Yellow
    }
    else {
        foreach ($target in $targets) {
            Remove-Item $target -Recurse -Force
            Write-Host "Removed $target" -ForegroundColor Cyan
        }
    }

    $fileArtifacts = Get-ChildItem -Path $repoRoot -Recurse -Force -File -Include *.trx, *.coverage, *.coveragexml |
        Where-Object { $_.FullName -notlike "*\.git\*" } |
        Select-Object -ExpandProperty FullName |
        Sort-Object -Unique

    foreach ($artifact in $fileArtifacts) {
        Remove-Item $artifact -Force
        Write-Host "Removed $artifact" -ForegroundColor Cyan
    }

    Write-Host "Clean completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
