param(
    [switch]$DeployToGame,
    [Alias("DisableOtherMods")]
    [switch]$IsolateMods,
    [Alias("CleanGameMods")]
    [switch]$RemoveDeployedMods,
    [Alias("Restore")]
    [switch]$RestoreSettings,
    [switch]$CleanupTest,
    [switch]$CleanDist,
    [switch]$BuildOnly,
    [switch]$Interactive,
    [switch]$Force,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$vsDir = if ($env:VINTAGE_STORY) { $env:VINTAGE_STORY } else { Join-Path $env:APPDATA "Vintagestory" }
$vsDataDir = if ($env:VINTAGE_STORY_DATA) { $env:VINTAGE_STORY_DATA } else { Join-Path $env:APPDATA "VintagestoryData" }
$vsDataMods = Join-Path $vsDataDir "Mods"
$clientSettingsPath = Join-Path $vsDataDir "clientsettings.json"
$backupPath = Join-Path $vsDataDir "clientsettings.disabledMods.backup.json"
$distDir = Join-Path $repoRoot "dist"
$ownModIds = @("seraphui", "tradersoverhaul", "systemui", "comerciantes")
$ownZipNames = @("seraphui.zip", "tradersoverhaul.zip")
$legacyZipNames = @("systemui.zip", "comerciantes.zip")

function Test-GameRunning {
    return $null -ne (Get-Process -Name "Vintagestory" -ErrorAction SilentlyContinue)
}

function Assert-GameNotRunning {
    if ($Force) { return }
    if (Test-GameRunning) {
        throw "Vintage Story esta en ejecucion. Cierralo antes de modificar clientsettings.json (o usa -Force)."
    }
}

function Load-Newtonsoft {
    $newtonDll = Join-Path $vsDir "Lib\Newtonsoft.Json.dll"
    if (-not (Test-Path $newtonDll)) {
        throw "No se encontro Newtonsoft.Json.dll en: $newtonDll"
    }
    if (-not ([System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq "Newtonsoft.Json" })) {
        [System.Reflection.Assembly]::LoadFrom($newtonDll) | Out-Null
    }
}

function Read-ModKeyFromJson([string]$jsonText) {
    $obj = [Newtonsoft.Json.Linq.JObject]::Parse($jsonText)
    $modidProp = $obj.Property("modid", [System.StringComparison]::OrdinalIgnoreCase)
    $verProp = $obj.Property("version", [System.StringComparison]::OrdinalIgnoreCase)
    $modid = if ($modidProp) { $modidProp.Value.ToString().Trim().ToLowerInvariant() } else { "" }
    $ver = if ($verProp) { $verProp.Value.ToString().Trim() } else { "" }
    if (-not $modid) { return $null }
    return [PSCustomObject]@{
        ModId   = $modid
        Version = $ver
        Key     = if ($ver) { "$modid@$ver" } else { $modid }
    }
}

function Get-InstalledUserMods {
    $results = @()
    if (-not (Test-Path $vsDataMods)) { return $results }

    foreach ($zip in (Get-ChildItem $vsDataMods -Filter "*.zip" -File)) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip.FullName)
        try {
            $entry = $archive.Entries | Where-Object { $_.FullName -ieq "modinfo.json" } | Select-Object -First 1
            if (-not $entry) {
                $entry = $archive.Entries | Where-Object { $_.Name -ieq "modinfo.json" } | Sort-Object { $_.FullName.Length } | Select-Object -First 1
            }
            if (-not $entry) { continue }
            $sr = New-Object System.IO.StreamReader($entry.Open())
            try {
                $parsed = Read-ModKeyFromJson ($sr.ReadToEnd())
                if ($parsed) { $results += $parsed }
            } finally {
                $sr.Close()
            }
        } finally {
            $archive.Dispose()
        }
    }

    foreach ($dir in (Get-ChildItem $vsDataMods -Directory)) {
        $mi = Join-Path $dir.FullName "modinfo.json"
        if (Test-Path $mi) {
            $parsed = Read-ModKeyFromJson (Get-Content $mi -Raw)
            if ($parsed) { $results += $parsed }
        }
    }

    return $results
}

function Update-DisabledMods([scriptblock]$transform) {
    if (-not (Test-Path $clientSettingsPath)) {
        throw "No se encontro clientsettings.json en: $clientSettingsPath"
    }
    Load-Newtonsoft

    $root = [Newtonsoft.Json.Linq.JObject]::Parse((Get-Content $clientSettingsPath -Raw))
    $slProp = $root.Property("stringListSettings", [System.StringComparison]::OrdinalIgnoreCase)
    if (-not $slProp -or -not ($slProp.Value -is [Newtonsoft.Json.Linq.JObject])) {
        $newSl = New-Object Newtonsoft.Json.Linq.JObject
        if ($slProp) { $slProp.Value = $newSl } else { $root.Add("stringListSettings", $newSl) }
        $slProp = $root.Property("stringListSettings", [System.StringComparison]::OrdinalIgnoreCase)
    }

    $sl = $slProp.Value
    $dmProp = $sl.Property("disabledMods", [System.StringComparison]::OrdinalIgnoreCase)
    $current = @()
    if ($dmProp -and $dmProp.Value -is [Newtonsoft.Json.Linq.JArray]) {
        foreach ($x in $dmProp.Value) { $current += $x.ToString() }
    }

    $next = @(& $transform $current)

    $jArr = New-Object Newtonsoft.Json.Linq.JArray
    foreach ($item in $next) {
        $jArr.Add([Newtonsoft.Json.Linq.JValue]::new([string]$item))
    }
    if ($dmProp) {
        $dmProp.Value = $jArr
    } else {
        $sl.Add("disabledMods", $jArr)
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($clientSettingsPath, $root.ToString([Newtonsoft.Json.Formatting]::Indented), $utf8NoBom)
}

function Invoke-BuildAndPackage([bool]$deployToUserMods, [string]$config) {
    $mods = @(
        @{ Name = "seraphui"; OutDir = Join-Path $repoRoot "mods\system-ui\bin\$config\net10.0" },
        @{ Name = "tradersoverhaul"; OutDir = Join-Path $repoRoot "mods\comerciantes\bin\$config\net10.0" }
    )

    foreach ($mod in $mods) {
        if (Test-Path $mod.OutDir) {
            Remove-Item $mod.OutDir -Recurse -Force
        }
    }

    Write-Host "Compilando solucion ($config)..." -ForegroundColor Cyan
    dotnet build (Join-Path $repoRoot "VintageStoryMods.sln") -c $config -m:1 -p:VintageStoryDir="$vsDir"
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo la compilacion de VintageStoryMods.sln"
    }

    New-Item -ItemType Directory -Path $distDir -Force | Out-Null

    foreach ($legacyZip in $legacyZipNames) {
        $oldDist = Join-Path $distDir $legacyZip
        if (Test-Path $oldDist) { Remove-Item $oldDist -Force }
    }

    foreach ($mod in $mods) {
        $zipPath = Join-Path $distDir "$($mod.Name).zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $mod.OutDir "*") -DestinationPath $zipPath
        Write-Host "Empaquetado: $zipPath" -ForegroundColor Green

        if ($deployToUserMods) {
            New-Item -ItemType Directory -Path $vsDataMods -Force | Out-Null
            foreach ($legacyZip in $legacyZipNames) {
                $oldDeployed = Join-Path $vsDataMods $legacyZip
                if (Test-Path $oldDeployed) {
                    Remove-Item $oldDeployed -Force
                    Write-Host "Eliminado zip legado: $oldDeployed" -ForegroundColor Yellow
                }
            }
            Copy-Item $zipPath -Destination $vsDataMods -Force
            Write-Host "Copiado a: $vsDataMods\$($mod.Name).zip" -ForegroundColor Green
        }
    }
}

function Invoke-IsolateMods {
    Assert-GameNotRunning
    $installed = Get-InstalledUserMods

    Update-DisabledMods {
        param([string[]]$currentDisabled)

        if (-not (Test-Path $backupPath)) {
            $backupObj = [PSCustomObject]@{
                CreatedAt    = (Get-Date).ToString("o")
                DisabledMods = $currentDisabled
            }
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($backupPath, ($backupObj | ConvertTo-Json -Depth 5), $utf8NoBom)
            Write-Host "Respaldo de disabledMods guardado en: $backupPath ($($currentDisabled.Count) entradas)" -ForegroundColor Yellow
        } else {
            Write-Host "Usando respaldo existente en: $backupPath (no se sobrescribe)" -ForegroundColor Yellow
        }

        $newDisabled = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $currentDisabled) {
            $modId = ($entry -split "@")[0].ToLowerInvariant()
            if ($ownModIds -notcontains $modId) {
                [void]$newDisabled.Add($entry)
            }
        }

        foreach ($mod in $installed) {
            if ($ownModIds -contains $mod.ModId) { continue }
            [void]$newDisabled.Add($mod.Key)
        }

        return @($newDisabled | Sort-Object)
    }

    $isolatedCount = ($installed | Where-Object { $ownModIds -notcontains $_.ModId }).Count
    Write-Host "Aislamiento activo: $isolatedCount mods de terceros desactivados temporalmente; habilitados solo seraphui y tradersoverhaul (mas vanilla)." -ForegroundColor Green
}

function Invoke-CleanDist {
    if (-not (Test-Path $distDir)) { return }
    foreach ($zipName in ($ownZipNames + $legacyZipNames)) {
        $distZip = Join-Path $distDir $zipName
        if (Test-Path $distZip) {
            Remove-Item $distZip -Force
            Write-Host "Eliminado de dist: $distZip" -ForegroundColor Green
        }
    }
}

function Invoke-RemoveDeployedMods([bool]$alsoCleanDist) {
    foreach ($zipName in ($ownZipNames + $legacyZipNames)) {
        $target = Join-Path $vsDataMods $zipName
        if (Test-Path $target) {
            Remove-Item $target -Force
            Write-Host "Eliminado de Mods del juego: $target" -ForegroundColor Green
        } elseif ($ownZipNames -contains $zipName) {
            Write-Host "No encontrado en Mods del juego (omitido): $target" -ForegroundColor DarkGray
        }
    }

    if ($alsoCleanDist) {
        Invoke-CleanDist
    }
}

function Invoke-RestoreSettings {
    Assert-GameNotRunning
    if (-not (Test-Path $backupPath)) {
        Write-Host "No existe respaldo en $backupPath; no se modifico clientsettings.json." -ForegroundColor Yellow
        return
    }

    $backupData = Get-Content $backupPath -Raw | ConvertFrom-Json
    $restoredList = @()
    if ($backupData.DisabledMods) {
        $restoredList = @($backupData.DisabledMods | ForEach-Object { $_.ToString() })
    }

    Update-DisabledMods {
        param([string[]]$_)
        return $restoredList
    }

    Remove-Item $backupPath -Force
    Write-Host "Configuracion disabledMods restaurada ($($restoredList.Count) entradas) y respaldo eliminado." -ForegroundColor Green
}

function Wait-KeyContinue {
    Write-Host ""
    Write-Host "Presiona cualquier tecla para volver al menu..." -ForegroundColor DarkGray
    [void][System.Console]::ReadKey($true)
}

function Show-CliMenu {
    $optBuild = $true
    $optDeploy = $true
    $optIsolate = $true
    $optRelease = ($Configuration -eq "Release")
    $optCleanDist = $false
    $selectedIndex = 4 # Apunta directo a "Aplicar configuracion seleccionada"

    while ($true) {
        $configName = if ($optRelease) { "Release" } else { "Debug" }
        $gameRunning = Test-GameRunning
        $sysUiDeployed = Test-Path (Join-Path $vsDataMods "seraphui.zip")
        $comDeployed = Test-Path (Join-Path $vsDataMods "tradersoverhaul.zip")
        $hasBackup = Test-Path $backupPath

        $items = @(
            @{ Kind = "Toggle"; Key = "Build";     Label = "Compilar solucion y empaquetar en dist\";                    Value = $optBuild },
            @{ Kind = "Toggle"; Key = "Deploy";    Label = "Desplegar .zip en VintagestoryData\Mods";                   Value = $optDeploy },
            @{ Kind = "Toggle"; Key = "Isolate";   Label = "Aislar mods (desactivar terceros temporalmente)";            Value = $optIsolate },
            @{ Kind = "Toggle"; Key = "Release";   Label = "Modo Release (desactivado = Debug con logs en juego)";        Value = $optRelease },
            @{ Kind = "Action"; Key = "Apply";     Label = "Ejecutar configuracion seleccionada" },
            @{ Kind = "Toggle"; Key = "CleanDist"; Label = "Tambien limpiar dist\ al eliminar mods";                    Value = $optCleanDist },
            @{ Kind = "Action"; Key = "Remove";    Label = "Eliminar mods compilados de VintagestoryData\Mods" },
            @{ Kind = "Action"; Key = "Restore";   Label = "Restaurar clientsettings.json desde respaldo" },
            @{ Kind = "Action"; Key = "Cleanup";   Label = "Limpiar todo (quitar mods desplegados + restaurar settings)" },
            @{ Kind = "Action"; Key = "Exit";      Label = "Salir" }
        )

        Clear-Host
        Write-Host "=================================================================" -ForegroundColor DarkCyan
        Write-Host "      Vintage Story Mods - Gestor Interactivo por Teclado        " -ForegroundColor White
        Write-Host "=================================================================" -ForegroundColor DarkCyan
        Write-Host ("  Juego     : {0}" -f $(if ($gameRunning) { "EN EJECUCION (cierra antes de cambiar settings)" } else { "Cerrado" })) -ForegroundColor $(if ($gameRunning) { "Red" } else { "Green" })
        Write-Host ("  Mods      : seraphui.zip={0} | tradersoverhaul.zip={1}" -f $(if ($sysUiDeployed) { "SI" } else { "NO" }), $(if ($comDeployed) { "SI" } else { "NO" }))
        Write-Host ("  Respaldo  : {0}" -f $(if ($hasBackup) { "ACTIVO (mods aislados)" } else { "Ninguno" })) -ForegroundColor $(if ($hasBackup) { "Yellow" } else { "DarkGray" })
        Write-Host "-----------------------------------------------------------------" -ForegroundColor DarkCyan
        Write-Host "  CONFIGURACION Y ACCIONES:" -ForegroundColor Gray

        for ($i = 0; $i -lt $items.Count; $i++) {
            $item = $items[$i]
            $isSel = ($i -eq $selectedIndex)
            $cursor = if ($isSel) { " > " } else { "   " }

            if ($item.Kind -eq "Toggle") {
                $box = if ($item.Value) { "[x]" } else { "[ ]" }
                $line = "$cursor$box $($item.Label)"
                $color = if ($isSel) { "Cyan" } elseif ($item.Value) { "Green" } else { "DarkGray" }
                Write-Host $line -ForegroundColor $color
            } else {
                $prefix = if ($item.Key -eq "Apply") { ">> " } else { "   " }
                $line = "$cursor$prefix$($item.Label)"
                $color = if ($isSel) { "Yellow" } else { "White" }
                Write-Host $line -ForegroundColor $color
            }

            if ($i -eq 4) {
                Write-Host "  ---------------------------------------------------------------" -ForegroundColor DarkGray
            }
        }

        Write-Host "=================================================================" -ForegroundColor DarkCyan
        Write-Host "  Flechas Arriba/Abajo: Mover | Tab / Espacio: Activar/Desactivar" -ForegroundColor DarkGray
        Write-Host "  Enter: Ejecutar accion        | Esc / Q: Salir" -ForegroundColor DarkGray

        $keyInfo = [System.Console]::ReadKey($true)
        $current = $items[$selectedIndex]

        switch ($keyInfo.Key) {
            "UpArrow" {
                $selectedIndex = if ($selectedIndex -le 0) { $items.Count - 1 } else { $selectedIndex - 1 }
            }
            "DownArrow" {
                $selectedIndex = if ($selectedIndex -ge ($items.Count - 1)) { 0 } else { $selectedIndex + 1 }
            }
            "Tab" {
                if ($current.Kind -eq "Toggle") {
                    switch ($current.Key) {
                        "Build"     { $optBuild = -not $optBuild }
                        "Deploy"    { $optDeploy = -not $optDeploy }
                        "Isolate"   { $optIsolate = -not $optIsolate }
                        "Release"   { $optRelease = -not $optRelease }
                        "CleanDist" { $optCleanDist = -not $optCleanDist }
                    }
                } else {
                    $selectedIndex = 0
                }
            }
            "Spacebar" {
                if ($current.Kind -eq "Toggle") {
                    switch ($current.Key) {
                        "Build"     { $optBuild = -not $optBuild }
                        "Deploy"    { $optDeploy = -not $optDeploy }
                        "Isolate"   { $optIsolate = -not $optIsolate }
                        "Release"   { $optRelease = -not $optRelease }
                        "CleanDist" { $optCleanDist = -not $optCleanDist }
                    }
                }
            }
            "Enter" {
                if ($current.Kind -eq "Toggle") {
                    $selectedIndex = 4
                    continue
                }

                Clear-Host
                try {
                    switch ($current.Key) {
                        "Apply" {
                            if ($optBuild) {
                                Invoke-BuildAndPackage -deployToUserMods $optDeploy -config $configName
                            } elseif ($optDeploy) {
                                foreach ($zipName in $ownZipNames) {
                                    $src = Join-Path $distDir $zipName
                                    if (-not (Test-Path $src)) { throw "No existe $src. Activa 'Compilar' primero." }
                                    New-Item -ItemType Directory -Path $vsDataMods -Force | Out-Null
                                    Copy-Item $src -Destination $vsDataMods -Force
                                    Write-Host "Copiado a: $vsDataMods\$zipName" -ForegroundColor Green
                                }
                            }
                            if ($optIsolate) {
                                Invoke-IsolateMods
                            } elseif (Test-Path $backupPath) {
                                Invoke-RestoreSettings
                            }
                            if (-not $optBuild -and -not $optDeploy -and -not $optIsolate -and -not (Test-Path $backupPath)) {
                                Write-Host "No hay opciones activas en configuracion." -ForegroundColor Yellow
                            }
                        }
                        "Remove" {
                            Invoke-RemoveDeployedMods -alsoCleanDist $optCleanDist
                        }
                        "Restore" {
                            Invoke-RestoreSettings
                        }
                        "Cleanup" {
                            Invoke-RemoveDeployedMods -alsoCleanDist $optCleanDist
                            Invoke-RestoreSettings
                        }
                        "Exit" {
                            return
                        }
                    }
                } catch {
                    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
                }

                Wait-KeyContinue
            }
            "Escape" { return }
            "Q"      { return }
        }
    }
}

if ($CleanupTest) {
    $RemoveDeployedMods = $true
    $RestoreSettings = $true
}

$hasActionSwitch = $DeployToGame -or $IsolateMods -or $RemoveDeployedMods -or $RestoreSettings -or $CleanDist -or $BuildOnly

if ($Interactive -or -not $hasActionSwitch) {
    Show-CliMenu
    exit 0
}

if ($BuildOnly -or $DeployToGame) {
    Invoke-BuildAndPackage -deployToUserMods ([bool]$DeployToGame) -config $Configuration
}

if ($IsolateMods) {
    Invoke-IsolateMods
}

if ($RemoveDeployedMods) {
    Invoke-RemoveDeployedMods -alsoCleanDist $false
}

if ($CleanDist) {
    Invoke-CleanDist
}

if ($RestoreSettings) {
    Invoke-RestoreSettings
}
