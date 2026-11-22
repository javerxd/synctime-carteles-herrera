# Fase 16A: crea el archivo .env para compose con claves al azar (version para Windows de scripts/crear-env.sh).
# No sobrescribe uno que ya exista. Uso, desde la raiz del repositorio:  .\scripts\crear-env.ps1
$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz '.env'
$molde = Join-Path $raiz '.env.example'

if (Test-Path $destino) {
    Write-Warning "Ya existe $destino`: no se cambia nada. Borrelo si quiere generarlo de nuevo."
    return
}
if (-not (Test-Path $molde)) { throw "Falta $molde." }

# Solo letras y numeros: la clave de SQL Server va dentro de una cadena de conexion y no admite ';'.
$generador = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function New-ClaveAlfanumerica([int]$largo) {
    $caracteres = [char[]]"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
    $clave = New-Object System.Text.StringBuilder
    $bytes = [byte[]]::new(1)
    while ($clave.Length -lt $largo) {
        $generador.GetBytes($bytes)
        # Se descartan los bytes de 248 en adelante para que todos los caracteres salgan con la misma probabilidad.
        if ($bytes[0] -lt 248) { [void]$clave.Append($caracteres[$bytes[0] % $caracteres.Length]) }
    }
    $clave.ToString()
}

$sa = (New-ClaveAlfanumerica 20) + 'Aa1'
$jwt = New-ClaveAlfanumerica 48
$demo = (New-ClaveAlfanumerica 14) + 'Aa1-'
$bytesRostro = [byte[]]::new(32)
$generador.GetBytes($bytesRostro)
$rostro = [Convert]::ToBase64String($bytesRostro)

$lineas = Get-Content $molde | ForEach-Object {
    switch -Regex ($_) {
        '^MSSQL_SA_PASSWORD=' { "MSSQL_SA_PASSWORD=$sa" }
        '^JWT_KEY='           { "JWT_KEY=$jwt" }
        '^DEMO_PASSWORD='     { "DEMO_PASSWORD=$demo" }
        '^ROSTRO_CLAVE='      { "ROSTRO_CLAVE=$rostro" }
        default               { $_ }
    }
}
# UTF-8 sin BOM: compose lee el archivo tal cual.
[System.IO.File]::WriteAllLines($destino, $lineas, [System.Text.UTF8Encoding]::new($false))

Write-Host "Creado $destino."
Write-Host 'Usuarios de demostracion: dueno, admin, disenador, operario y cliente, todos @letreria.test'
Write-Host "Clave de todos ellos: $demo"
Write-Host 'Ahora:  docker compose up --build   y abra http://localhost:8080'
