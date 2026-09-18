$ErrorActionPreference = 'Stop'

# Synchronisation differentielle des sources de compilation.
# Le script ne touche jamais a jeu\game ni a l'executable du launcher.
# Le SHA du blob GitHub est compare au SHA Git (blob) local : une virgule
# modifiee telecharge donc uniquement le fichier qui la contient.
function Get-GitBlobSha([string] $path) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    [byte[]] $contenu = [System.IO.File]::ReadAllBytes($path)
    [byte[]] $entete = [System.Text.Encoding]::UTF8.GetBytes(('blob ' + $contenu.Length + [char]0))
    $flux = New-Object System.IO.MemoryStream
    $flux.Write($entete, 0, $entete.Length)
    $flux.Write($contenu, 0, $contenu.Length)
    $flux.Position = 0
    $sha = [System.Security.Cryptography.SHA1]::Create().ComputeHash($flux)
    $flux.Dispose()
    return (($sha | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Get-LocalPath([string] $remote) {
    $barre = $remote.Replace('/', '\')
    if ($barre.StartsWith('compilation\')) {
        return Join-Path $env:LV_ROOT $barre.Substring(12)
    }
    if ($barre.StartsWith('jeu\')) {
        return Join-Path $env:LV_JEU $barre.Substring(4)
    }
    return $null
}

function Est-Source([string] $remote) {
    $p = $remote.Replace('/', '\')
    if (!($p.StartsWith('compilation\') -or $p.StartsWith('jeu\'))) { return $false }
    foreach ($ignore in @('\image\', '\Library\', '\Temp\', '\Logs\', '\obj\', '\Build\', '\build\', '\game\', '\game.ancien\', '\game.install\')) {
        if ($p.Contains($ignore)) { return $false }
    }
    if ($p.EndsWith('\LibreVies.exe') -or $p.EndsWith('\jeu.download')) { return $false }
    return $true
}

try {
    $brancheApi = [uri]::EscapeDataString($env:LV_BRANCHE)
    $baseApi = 'https://api.github.com/repos/' + $env:LV_DEPOT
    $headers = @{ 'User-Agent' = 'LibreVies-build' }
    Write-Host '        lecture du commit GitHub...'
    $commit = Invoke-RestMethod -Uri ($baseApi + '/commits/' + $brancheApi) -Headers $headers -TimeoutSec 30
    $commitSha = $commit.sha
    $tree = Invoke-RestMethod -Uri ($baseApi + '/git/trees/' + $commit.commit.tree.sha + '?recursive=1') -Headers $headers -TimeoutSec 60
    if ($tree.truncated) { throw 'arbre GitHub tronque : synchronisation refusee pour securite' }

    $telecharges = 0
    $deja = 0
    $total = 0
    foreach ($item in $tree.tree) {
        if ($item.type -ne 'blob' -or !(Est-Source $item.path)) { continue }
        $total++
        $local = Get-LocalPath $item.path
        if (!$local) { continue }
        $actuel = Get-GitBlobSha $local
        if ($actuel -eq $item.sha) { $deja++; continue }

        $segments = $item.path.Split('/') | ForEach-Object { [uri]::EscapeDataString($_) }
        $raw = 'https://raw.githubusercontent.com/' + $env:LV_DEPOT + '/' + $env:LV_BRANCHE + '/' + ($segments -join '/')
        $destination = $local
        # Un .bat ne peut pas s'ecraser alors qu'il execute : il sera applique
        # par l'intermediaire deja present dans build_launcher.bat.
        if ($item.path -eq 'compilation/build_launcher.bat') {
            $destination = Join-Path $env:LV_ROOT 'build_launcher.bat.maj'
            $env:LV_SCRIPT_CHANGE = 'oui'
        }
        $parent = Split-Path -Parent $destination
        if (!(Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        Write-Host ('        telechargement : ' + $item.path)
        Invoke-WebRequest -Uri $raw -OutFile $destination -Headers $headers -UseBasicParsing -TimeoutSec 60
        $telecharges++
    }
    if ($env:LV_SCRIPT_CHANGE -eq 'oui') {
        Set-Content -Path (Join-Path $env:LV_ROOT 'build\script_recu.txt') -Value 'oui' -Encoding ASCII
    }
    Set-Content -Path (Join-Path $env:LV_ROOT 'build\branche_actuelle.txt') -Value $commitSha -Encoding ASCII
    Write-Host ('        sources : ' + $telecharges + ' telecharge(s), ' + $deja + ' deja a jour sur ' + $total + ' fichier(s).')
    exit 0
}
catch {
    Write-Host ('        ERREUR synchronisation differentielle : ' + $_.Exception.Message)
    exit 2
}
