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

$entries = @(Get-ChildItem $Root -File)
if ($entries.Count -lt 1) {
    throw "El bundle publicado está vacío: $Root"
}

if (-not ($entries.Name -contains 'SysDiag.exe')) {
    throw "El bundle publicado no contiene el ejecutable principal: SysDiag.exe"
}

$required = @('SysDiag.exe')
foreach ($name in $required) {
    $path = Join-Path $Root $name
    if (-not (Test-Path $path)) {
        throw "Falta un artefacto crítico para el paquete: $name"
    }
}

Write-Host "Paquete de release validado correctamente: $Root"
Write-Host "Ejecutable: $exe"
Write-Host "Archivos: $($entries.Count)"
