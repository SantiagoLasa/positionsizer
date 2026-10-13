<#
    deploy-to-nt8.ps1
    Copia los archivos fuente del indicador a la carpeta Custom\Indicators de NinjaTrader 8.
    Uso:
        powershell -ExecutionPolicy Bypass -File scripts\deploy-to-nt8.ps1
    Luego en NinjaTrader 8: NinjaScript Editor -> Compilar (F5).
#>

$ErrorActionPreference = "Stop"

# Carpeta del proyecto (un nivel arriba de /scripts)
$projectRoot = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $projectRoot "src"

$files = @("PositionSizerMath.cs", "PositionSizerRR.cs")

# Candidatos a la carpeta de documentos de NinjaTrader 8 (incluye OneDrive y nombres en español)
$candidates = @(
    (Join-Path $env:USERPROFILE "OneDrive\Documentos\NinjaTrader 8"),
    (Join-Path $env:USERPROFILE "OneDrive\Documents\NinjaTrader 8"),
    (Join-Path $env:USERPROFILE "Documentos\NinjaTrader 8"),
    (Join-Path $env:USERPROFILE "Documents\NinjaTrader 8"),
    (Join-Path ([Environment]::GetFolderPath("MyDocuments")) "NinjaTrader 8")
)

$ntBase = $null
foreach ($c in $candidates) {
    if (Test-Path (Join-Path $c "bin\Custom\Indicators")) { $ntBase = $c; break }
}

if ($null -eq $ntBase) {
    Write-Host "No encontre la carpeta 'NinjaTrader 8\bin\Custom\Indicators'." -ForegroundColor Red
    Write-Host "Pasa la ruta base a mano, por ejemplo:" -ForegroundColor Yellow
    Write-Host '   $dest = "C:\ruta\a\NinjaTrader 8\bin\Custom\Indicators"' -ForegroundColor Yellow
    exit 1
}

$dest = Join-Path $ntBase "bin\Custom\Indicators"
Write-Host "Destino: $dest" -ForegroundColor Cyan

foreach ($f in $files) {
    $from = Join-Path $srcDir $f
    if (-not (Test-Path $from)) { throw "No existe el archivo fuente: $from" }
    Copy-Item -Path $from -Destination (Join-Path $dest $f) -Force
    Write-Host "  copiado: $f" -ForegroundColor Green
}

Write-Host ""
Write-Host "Listo. Ahora en NinjaTrader 8:" -ForegroundColor Cyan
Write-Host "  1) Abri el NinjaScript Editor (New -> NinjaScript Editor)."
Write-Host "  2) Compila con F5 (o boton Compile)."
Write-Host "  3) Agrega el indicador 'PositionSizerRR' a un grafico de futuros."
