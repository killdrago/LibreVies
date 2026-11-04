$ErrorActionPreference = 'Stop'

# Synchronisation differentielle des sources de compilation.
# Le script ne touche jamais a jeu\game ni a l'executable du launcher.
#
# Deux acces sont utilises, dans cet ordre :
#
#   1. le depot Git local, toujours essaye en premier. Le build vient de
#      faire "git fetch" : le commit distant et tous ses blobs sont donc
#      deja dans .git, avec les identifiants Git enregistres sur le poste.
#      Aucune requete HTTP n'est faite, donc la synchronisation fonctionne
#      aussi quand le depot GitHub est PRIVE : une lecture anonyme de
#      raw.githubusercontent.com ou de api.github.com y repond 404
#      "Introuvable".
#   2. l'API GitHub, avec un jeton quand le poste en fournit un
#      (LV_GH_TOKEN, GITHUB_TOKEN, GH_TOKEN, sinon les identifiants Git
#      enregistres). Ce chemin ne sert plus que de secours, quand la base
#      Git locale n'est pas exploitable.
#
# Dans les deux cas le SHA du blob GitHub est compare au SHA Git (blob)
# calcule sur le fichier local : une virgule modifiee ne remplace donc que
# le fichier qui la contient.

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$ExtensionsTexte = @(
    '.pyw', '.py', '.bat', '.cmd', '.json', '.cfg', '.txt', '.md', '.html',
    '.css', '.js', '.mjs', '.csv', '.cs', '.shader', '.hlsl', '.cginc',
    '.meta', '.unity', '.asset', '.prefab', '.mat', '.yaml', '.yml', '.ini',
    '.php', '.sql', '.ps1', '.shadergraph', '.inputactions'
)

function Get-GitBlobSha([string] $path, [switch] $NormaliserLf) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    [byte[]] $contenu = [System.IO.File]::ReadAllBytes($path)
    if ($NormaliserLf) {
        # Latin1 : conversion octet pour octet, donc sans aucune perte.
        # Sert uniquement a ignorer une difference de fin de ligne : le fichier
        # local en CRLF et le blob GitHub en LF designent le meme contenu.
        $latin1 = [System.Text.Encoding]::GetEncoding(28591)
        $texte = $latin1.GetString($contenu) -replace "`r`n", "`n"
        $contenu = $latin1.GetBytes($texte)
    }
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

function Est-Texte([string] $remote) {
    $extension = [System.IO.Path]::GetExtension($remote)
    if (!$extension) { return $false }
    return ($ExtensionsTexte -contains $extension.ToLowerInvariant())
}

function Get-Git {
    if ($env:LV_GIT) { return $env:LV_GIT }
    return 'git'
}

function Get-RacineGit {
    $racine = $env:LV_GIT_ROOT
    if (!$racine) { return $null }
    if (!(Test-Path -LiteralPath (Join-Path $racine '.git'))) { return $null }
    return $racine
}

function Get-CommitLocal([string] $racine) {
    foreach ($reference in @('HEAD', 'FETCH_HEAD')) {
        $lignes = @(& (Get-Git) -C $racine rev-parse --verify ($reference + '^{commit}') 2>$null)
        if ($LASTEXITCODE -eq 0) {
            foreach ($ligne in $lignes) {
                $valeur = "$ligne".Trim()
                if ($valeur -match '^[0-9a-f]{40,64}$') { return $valeur }
            }
        }
    }
    return ''
}

function Write-GitBlob([string] $racine, [string] $sha, [string] $destination) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = (Get-Git)
    $info.Arguments = '-C "' + $racine + '" cat-file blob ' + $sha
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.CreateNoWindow = $true
    $processus = [System.Diagnostics.Process]::Start($info)
    $flux = [System.IO.File]::Create($destination)
    try {
        $processus.StandardOutput.BaseStream.CopyTo($flux)
    } finally {
        $flux.Close()
    }
    $erreur = $processus.StandardError.ReadToEnd()
    $processus.WaitForExit()
    if ($processus.ExitCode -ne 0) {
        throw ('git cat-file a echoue : ' + $erreur.Trim())
    }
}

function Set-FinDeLigneWindows([string] $chemin) {
    # Les .bat du depot sont declares "*.bat text eol=crlf" : on respecte ce
    # contrat pour qu'un script recopie reste executable par cmd.exe.
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $texte = $latin1.GetString([System.IO.File]::ReadAllBytes($chemin))
    $texte = $texte -replace "`r`n", "`n"
    $texte = $texte -replace "`n", "`r`n"
    [System.IO.File]::WriteAllBytes($chemin, $latin1.GetBytes($texte))
}

function Sync-DepuisGit([string] $racine) {
    $commit = Get-CommitLocal $racine
    if (!$commit) { return $null }

    $lignes = @(& (Get-Git) -c core.quotePath=false -C $racine ls-tree -r $commit 2>$null)
    if ($LASTEXITCODE -ne 0 -or $lignes.Count -eq 0) { return $null }

    $synchronises = 0
    $deja = 0
    $total = 0
    foreach ($ligne in $lignes) {
        $texte = "$ligne"
        $index = $texte.IndexOf([char]9)
        if ($index -lt 0) { continue }
        $entete = $texte.Substring(0, $index)
        $chemin = $texte.Substring($index + 1)
        $morceaux = $entete.Split(' ')
        if ($morceaux.Count -lt 3 -or $morceaux[1] -ne 'blob') { continue }
        $sha = $morceaux[2]
        if (!(Est-Source $chemin)) { continue }
        $local = Get-LocalPath $chemin
        if (!$local) { continue }
        $total++

        $texteLocal = Est-Texte $chemin
        $actuel = Get-GitBlobSha $local
        if ($actuel -ne $sha -and $texteLocal) { $actuel = Get-GitBlobSha $local -NormaliserLf }
        # Le script principal est critique pour la compilation Unity. On le
        # rafraichit toujours, meme si une ancienne copie locale annonce par
        # erreur le meme hash : cela evite de relancer Unity avec un C# obsolet.
        $forceScriptUnity = $chemin -eq 'compilation/unity/Assets/Scripts/LibreViesGame.cs'
        if (!$forceScriptUnity -and $actuel -eq $sha) { $deja++; continue }

        $destination = $local
        # Un .bat ne peut pas s'ecraser alors qu'il execute : il sera applique
        # par l'intermediaire deja present dans build_launcher.bat.
        if ($chemin -eq 'compilation/build_launcher.bat') {
            $destination = Join-Path $env:LV_ROOT 'build_launcher.bat.maj'
            $env:LV_SCRIPT_CHANGE = 'oui'
        }
        $parent = Split-Path -Parent $destination
        if (!(Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }

        Write-Host ('        extraction : ' + $chemin)
        $temporaire = $destination + '.librevies-tmp'
        try {
            Write-GitBlob $racine $sha $temporaire
            $verifie = Get-GitBlobSha $temporaire
            if ($verifie -ne $sha -and $texteLocal) { $verifie = Get-GitBlobSha $temporaire -NormaliserLf }
            if ($verifie -ne $sha) {
                throw ("hash local incorrect apres extraction : {0} (attendu {1}, obtenu {2})" -f $chemin, $sha, $verifie)
            }
            if ($chemin -like '*.bat' -or $chemin -like '*.cmd') { Set-FinDeLigneWindows $temporaire }
            Move-Item -Force -LiteralPath $temporaire -Destination $destination
        } finally {
            if (Test-Path -LiteralPath $temporaire) { Remove-Item -Force -LiteralPath $temporaire -ErrorAction SilentlyContinue }
        }
        $synchronises++
    }

    return @{ commit = $commit; synchronises = $synchronises; deja = $deja; total = $total; source = 'depot Git local' }
}

function Get-JetonGithub {
    foreach ($nom in @('LV_GH_TOKEN', 'GITHUB_TOKEN', 'GH_TOKEN')) {
        $valeur = [string][Environment]::GetEnvironmentVariable($nom)
        if ($valeur -and $valeur.Trim()) { return $valeur.Trim() }
    }
    try {
        $env:GCM_INTERACTIVE = 'Never'
        $sortie = @(("protocol=https`nhost=github.com`n`n") | & (Get-Git) -c credential.interactive=false credential fill 2>$null)
        foreach ($ligne in $sortie) {
            if ("$ligne" -match '^password=(.+)$') { return $Matches[1].Trim() }
        }
    } catch { }
    return ''
}

function Sync-DepuisGithub {
    $brancheApi = [uri]::EscapeDataString($env:LV_BRANCHE)
    $baseApi = 'https://api.github.com/repos/' + $env:LV_DEPOT
    $entetes = @{ 'User-Agent' = 'LibreVies-build' }
    $jeton = Get-JetonGithub
    if ($jeton) {
        # Un depot prive refuse toute lecture anonyme (404) : le jeton vient
        # des identifiants Git du poste, il n'y a rien a saisir de plus.
        $paire = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes('x-access-token:' + $jeton))
        $entetes['Authorization'] = 'Basic ' + $paire
    }
    Write-Host '        lecture du commit GitHub...'
    $commit = Invoke-RestMethod -Uri ($baseApi + '/commits/' + $brancheApi) -Headers $entetes -TimeoutSec 30
    $commitSha = $commit.sha
    $tree = Invoke-RestMethod -Uri ($baseApi + '/git/trees/' + $commit.commit.tree.sha + '?recursive=1') -Headers $entetes -TimeoutSec 60
    if ($tree.truncated) { throw 'arbre GitHub tronque : synchronisation refusee pour securite' }

    $synchronises = 0
    $deja = 0
    $total = 0
    foreach ($item in $tree.tree) {
        if ($item.type -ne 'blob' -or !(Est-Source $item.path)) { continue }
        $total++
        $local = Get-LocalPath $item.path
        if (!$local) { continue }
        $texteLocal = Est-Texte $item.path
        $actuel = Get-GitBlobSha $local
        if ($actuel -ne $item.sha -and $texteLocal) { $actuel = Get-GitBlobSha $local -NormaliserLf }
        # Le script principal est critique pour la compilation Unity. On le
        # rafraichit toujours, meme si une ancienne copie locale annonce par
        # erreur le meme hash : cela evite de relancer Unity avec un C# obsolet.
        $forceScriptUnity = $item.path -eq 'compilation/unity/Assets/Scripts/LibreViesGame.cs'
        if (!$forceScriptUnity -and $actuel -eq $item.sha) { $deja++; continue }

        $destination = $local
        # Un .bat ne peut pas s'ecraser alors qu'il execute : il sera applique
        # par l'intermediaire deja present dans build_launcher.bat.
        if ($item.path -eq 'compilation/build_launcher.bat') {
            $destination = Join-Path $env:LV_ROOT 'build_launcher.bat.maj'
            $env:LV_SCRIPT_CHANGE = 'oui'
        }
        $parent = Split-Path -Parent $destination
        if (!(Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        Write-Host ('        telechargement : ' + $item.path)

        $temporaire = $destination + '.librevies-tmp'
        try {
            if ($jeton) {
                # Les gros fichiers (textures Unity) depassent la limite de
                # l'API "contents" : on passe par les blobs, qui montent a 100 Mo.
                $blob = Invoke-RestMethod -Uri ($baseApi + '/git/blobs/' + $item.sha) -Headers $entetes -TimeoutSec 120
                [byte[]] $octets = [System.Convert]::FromBase64String(($blob.content -replace '\s', ''))
                [System.IO.File]::WriteAllBytes($temporaire, $octets)
            } else {
                # Depot public : la lecture anonyme fonctionne, on garde l'URL
                # du commit (jamais celle de la branche, que le CDN peut retarder).
                $segments = $item.path.Split('/') | ForEach-Object { [uri]::EscapeDataString($_) }
                $raw = 'https://raw.githubusercontent.com/' + $env:LV_DEPOT + '/' + $commitSha + '/' + ($segments -join '/')
                Invoke-WebRequest -Uri $raw -OutFile $temporaire -Headers $entetes -UseBasicParsing -TimeoutSec 60
            }
            $verifie = Get-GitBlobSha $temporaire
            if ($verifie -ne $item.sha -and $texteLocal) { $verifie = Get-GitBlobSha $temporaire -NormaliserLf }
            if ($verifie -ne $item.sha) {
                throw ("hash local incorrect apres telechargement : {0} (attendu {1}, obtenu {2})" -f $item.path, $item.sha, $verifie)
            }
            if ($item.path -like '*.bat' -or $item.path -like '*.cmd') { Set-FinDeLigneWindows $temporaire }
            Move-Item -Force -LiteralPath $temporaire -Destination $destination
        } finally {
            if (Test-Path -LiteralPath $temporaire) { Remove-Item -Force -LiteralPath $temporaire -ErrorAction SilentlyContinue }
        }
        $synchronises++
    }

    return @{ commit = $commitSha; synchronises = $synchronises; deja = $deja; total = $total; source = 'API GitHub' }
}

try {
    $dossierBuild = Join-Path $env:LV_ROOT 'build'
    if (!(Test-Path -LiteralPath $dossierBuild)) { New-Item -ItemType Directory -Force -Path $dossierBuild | Out-Null }

    $resultat = $null
    $racine = Get-RacineGit
    if ($racine) {
        try { $resultat = Sync-DepuisGit $racine }
        catch {
            Write-Host ('        base Git locale inutilisable : ' + $_.Exception.Message)
            $resultat = $null
        }
    }
    if (!$resultat) {
        Write-Host '        depot Git local inexploitable : lecture de GitHub...'
        $resultat = Sync-DepuisGithub
    }
    if ($env:LV_SCRIPT_CHANGE -eq 'oui') {
        Set-Content -Path (Join-Path $dossierBuild 'script_recu.txt') -Value 'oui' -Encoding ASCII
    }
    Set-Content -Path (Join-Path $dossierBuild 'branche_actuelle.txt') -Value $resultat.commit -Encoding ASCII
    Write-Host ('        sources : ' + $resultat.synchronises + ' synchronise(s), ' + $resultat.deja +
        ' deja a jour sur ' + $resultat.total + ' fichier(s), via ' + $resultat.source + '.')
    exit 0
}
catch {
    Write-Host ('        ERREUR synchronisation differentielle : ' + $_.Exception.Message)
    exit 2
}
