param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("patch", "minor", "major")]
    [string]$Tipo,
    [switch]$PushTag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj = Join-Path $root "SapaioDigital.App\SapaioDigital.App.csproj"
$versionFile = Join-Path $root "VERSION"

$atual = (Get-Content $versionFile -Raw).Trim()
$parts = $atual.Split('.')
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

switch ($Tipo) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}

$nova = "$major.$minor.$patch"
Set-Content -Path $versionFile -Value $nova -NoNewline -Encoding UTF8

$xml = Get-Content $csproj -Raw
$xml = $xml -replace '<Version>.*?</Version>', "<Version>$nova</Version>"
$xml = $xml -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$nova.0</AssemblyVersion>"
$xml = $xml -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$nova.0</FileVersion>"
[System.IO.File]::WriteAllText($csproj, $xml, [System.Text.UTF8Encoding]::new($false))

Write-Host "Versão atualizada: $atual -> $nova" -ForegroundColor Green

if ($PushTag) {
    git add VERSION SapaioDigital.App/SapaioDigital.App.csproj
    git commit -m "chore(release): v$nova"
    git tag "v$nova"
    git push origin main
    git push origin "v$nova"
    Write-Host "Tag v$nova enviada ao GitHub! O workflow de Release e Instalador foi disparado." -ForegroundColor Cyan
}
