param(
    [string]$Root = (Join-Path $PSScriptRoot "..\publish")
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Root)) {
    throw "No existe la carpeta publicada: $Root"
}

$exe = Join-Path $Root 'SysDiag.exe'
if (-not (Test-Path $exe)) {
    throw "Falta el ejecutable publicado: $exe"
}

$exeItem = Get-Item $exe
if ($exeItem.Length -lt 5MB) {
    throw "El ejecutable parece incompleto o demasiado pequeño: $($exeItem.Length) bytes"
}

$required = @(
    'SysDiag.dll',
    'System.Management.dll',
    'WindowsBase.dll',
    'PresentationCore.dll',
    'PresentationFramework.dll'
)

foreach ($name in $required) {
    $path = Join-Path $Root $name
    if (-not (Test-Path $path)) {
        throw "Falta un artefacto crítico para el paquete: $name"
    }
}

$entries = Get-ChildItem $Root -File | Select-Object -ExpandProperty Name
if ($entries.Count -lt 10) {
    throw "El bundle publicado parece incompleto: se esperaban más archivos que $($entries.Count)" 
}

Write-Host "Paquete de release validado correctamente: $Root"
Write-Host "Ejecutable: $exe"
Write-Host "Archivos: $($entries.Count)"
