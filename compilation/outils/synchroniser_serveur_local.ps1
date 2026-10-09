param([string] $Sources = (Join-Path $PSScriptRoot '..\..\jeu\serveur'))
$ErrorActionPreference = 'Stop'
$Sources = [IO.Path]::GetFullPath($Sources)

# Paquet unique : jeu/serveur. Synchronisation locale de ses fichiers publics
# seulement ; jamais de remplacement du vrai config.php ni d'import SQL.
$configAuth = Join-Path (Split-Path -Parent $Sources) 'auth_config.json'
if (Test-Path -LiteralPath $configAuth) {
    $auth = Get-Content -LiteralPath $configAuth -Raw | ConvertFrom-Json
    $url = [uri]$auth.api_url
    if ($url.Host -notin @('localhost', '127.0.0.1', '::1')) {
        Write-Host '        API distante : pas de synchronisation du serveur local.'
        exit 0
    }
    if ($url.AbsolutePath -ne '/serveur/api.php') {
        Write-Host '        API locale personnalisee : synchronisation standard ignoree.'
        exit 0
    }
}

$candidats = New-Object 'System.Collections.Generic.List[string]'
if ($env:LIBREVIES_WEB_ROOT) { $candidats.Add($env:LIBREVIES_WEB_ROOT) }

# Lire DocumentRoot dans la configuration de l'Apache actif (XAMPP/EasyPHP),
# sans afficher la ligne de commande ni lire les identifiants MySQL.
try {
    $processus = Get-CimInstance Win32_Process -Filter "Name='httpd.exe'" -ErrorAction Stop
    foreach ($apache in $processus) {
        if (!$apache.ExecutablePath) { continue }
        $racineApache = Split-Path -Parent (Split-Path -Parent $apache.ExecutablePath)
        $conf = Join-Path $racineApache 'conf\httpd.conf'
        if (!(Test-Path -LiteralPath $conf)) { continue }
        $texte = Get-Content -LiteralPath $conf -Raw
        $defines = @{}
        foreach ($definition in [regex]::Matches($texte, '(?m)^\s*Define\s+(\w+)\s+"?([^"\r\n]+)"?\s*$')) {
            $defines[$definition.Groups[1].Value] = $definition.Groups[2].Value.Trim()
        }
        $documentRoot = [regex]::Match($texte, '(?m)^\s*DocumentRoot\s+"([^"]+)"')
        if (!$documentRoot.Success) { continue }
        $chemin = $documentRoot.Groups[1].Value
        for ($i = 0; $i -lt 4; $i++) {
            foreach ($cle in $defines.Keys) {
                $chemin = $chemin.Replace('${' + $cle + '}', $defines[$cle])
            }
        }
        if ($chemin.Contains('${')) { continue }
        if (![IO.Path]::IsPathRooted($chemin)) { $chemin = Join-Path $racineApache $chemin }
        $candidats.Add($chemin)
    }
}
catch { Write-Host '        Detection Apache non disponible : recherche des dossiers standards.' }

# Le depot peut lui-meme etre place sous eds-www/htdocs/www.
$ancetre = Get-Item -LiteralPath $Sources
while ($ancetre -and $ancetre.Parent) {
    if ($ancetre.Name -in @('eds-www', 'htdocs', 'www')) { $candidats.Add($ancetre.FullName) }
    $ancetre = $ancetre.Parent
}
foreach ($lecteur in (Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Root -match '^[A-Za-z]:\\$' })) {
    foreach ($suffixe in @('xampp\htdocs', 'wamp64\www', 'wamp\www')) {
        $candidats.Add((Join-Path $lecteur.Root $suffixe))
    }
}
foreach ($parent in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, ($env:SystemDrive + '\'))) {
    if (!$parent -or !(Test-Path -LiteralPath $parent)) { continue }
    foreach ($easy in Get-ChildItem -LiteralPath $parent -Directory -Filter 'EasyPHP*' -ErrorAction SilentlyContinue) {
        $candidats.Add((Join-Path $easy.FullName 'eds-www'))
    }
}

$candidats = @($candidats | Select-Object -Unique | Where-Object { Test-Path -LiteralPath $_ -PathType Container })
$webRoot = $null
if ($env:LIBREVIES_WEB_ROOT -and (Test-Path -LiteralPath $env:LIBREVIES_WEB_ROOT -PathType Container)) {
    $webRoot = $env:LIBREVIES_WEB_ROOT
} else {
    # Priorite a l'API deja installee pour ne pas creer un serveur au hasard.
    $existants = @($candidats | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'serveur\api.php') })
    if ($existants.Count -eq 1) { $webRoot = $existants[0] }
    elseif ($existants.Count -eq 0 -and $candidats.Count -eq 1) { $webRoot = $candidats[0] }
}
if (!$webRoot) {
    Write-Host '        API locale non detectee : sources disponibles dans jeu\serveur.'
    Write-Host '        Un chemin personnalise peut etre defini par LIBREVIES_WEB_ROOT.'
    exit 0
}

$destination = Join-Path $webRoot 'serveur'
if ([IO.Path]::GetFullPath($Sources).TrimEnd('\') -eq [IO.Path]::GetFullPath($destination).TrimEnd('\')) {
    Write-Host '        API locale utilise deja les sources jeu\serveur.'
    exit 0
}
if (!(Test-Path -LiteralPath $destination)) { New-Item -ItemType Directory -Path $destination | Out-Null }
$nombre = 0
# Le helper doit arriver avant l'API qui l'inclut.
foreach ($nom in @('securite.php', 'personnage.php', 'npc.php', 'personnage.sql', 'npc.sql', 'classement.sql',
    'membre.sql', 'config.php.example', 'README.md', 'api.php')) {
    $source = Join-Path $Sources $nom
    $cible = Join-Path $destination $nom
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Source serveur absente : ' + $nom) }
    if ((Test-Path -LiteralPath $cible -PathType Leaf) -and
        (Get-FileHash -LiteralPath $source).Hash -eq (Get-FileHash -LiteralPath $cible).Hash) { continue }
    Copy-Item -LiteralPath $source -Destination $cible -Force
    $nombre++
}
# Retirer uniquement les anciennes migrations publiques connues : ni BDD,
# ni config.php, ni fichiers locaux inconnus ne sont touches.
foreach ($ancien in @('ajouter_droit.sql', 'remplir_npc.sql', 'mettre_a_jour_personnage.sql', 'corriger_membre.sql')) {
    $cibleAncienne = Join-Path $destination $ancien
    if (Test-Path -LiteralPath $cibleAncienne -PathType Leaf) {
        Remove-Item -LiteralPath $cibleAncienne -Force
        Write-Host ('        Ancien script SQL retire : ' + $ancien)
    }
}
Write-Host ('        API locale : ' + $nombre + ' fichier(s) mis a jour depuis jeu\serveur.')
Write-Host '        Configuration MySQL conservee : config.php n est jamais remplace.'
Write-Host '        Schemas actuels conserves dans jeu\serveur, sans import automatique.'
exit 0
