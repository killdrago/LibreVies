@echo off
setlocal EnableExtensions
rem ==========================================================================
rem  LibreVies - fabrication de la distribution (TOUT AUTOMATIQUE)
rem
rem  Ce script telecharge lui-meme tout ce qui manque, depuis GitHub :
rem    1. le projet (dossier unity + launcher) : les fichiers sont MIS A JOUR
rem       a chaque lancement depuis la branche GitHub ;
rem    2. Python (winget, sinon site officiel python.org) ;
rem    3. PyInstaller (necessaire une seule fois) ;
rem    4. Unity Hub + Unity Editor (via setup_unity_build_tools.bat).
rem  Puis il fabrique, DANS LE DOSSIER jeu\ (tout au meme endroit) :
rem    jeu\LibreVies.exe     = LE SEUL fichier a donner au joueur
rem    jeu\game\...          = le jeu exporte, a publier
rem
rem  Avant tout remplacement, l'ancienne version est copiee dans
rem  build\sauvegarde_locale\ : rien n'est jamais perdu.
rem  Etape suivante : outils\publier_jeu.bat (met le jeu en ligne)
rem ==========================================================================

rem --- Ou lire les sources (change la branche si besoin) -------------------
set "DEPOT=killdrago/LibreVies"
set "BRANCHE=arena/01a0b32c-librevies"
if defined LIBREVIES_BRANCHE set "BRANCHE=%LIBREVIES_BRANCHE%"
if not "%~1"=="" set "BRANCHE=%~1"

set "ROOT=%~dp0"
set "LAUNCHER=%ROOT%..\jeu\launcher.pyw"
set "AMORCE=%ROOT%amorce\amorce_launcher.py"
set "PROJECT=%ROOT%unity"
set "JEU=%ROOT%..\jeu"
set "BUILD=%ROOT%build"
set "PYTHON="
set "UNITY="
set "LV_ROOT=%ROOT%"
set "LV_JEU=%JEU%"
set "LV_DEPOT=%DEPOT%"
set "LV_BRANCHE=%BRANCHE%"

echo ==========================================================================
echo   LibreVies - fabrication de la distribution
echo ==========================================================================
echo   Projet  : %DEPOT%
echo   Branche : %BRANCHE%
echo   Dossier : %ROOT%
echo.
echo   Unity et Python ne servent QU'ICI : le joueur ne recoit que LibreVies.exe.
echo.

call :etape_projet
if errorlevel 1 goto :echec
call :etape_python
if errorlevel 1 goto :echec
call :etape_pyinstaller
if errorlevel 1 goto :echec
call :etape_unity
if errorlevel 1 goto :echec

rem --- Unity doit produire du Mono, pas de l'IL2CPP ------------------------
cd /d "%ROOT%"
echo.
echo [5/6] Export du jeu Unity...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Join-Path $env:PROJECT 'ProjectSettings\ProjectSettings.asset'; if (Test-Path $p) { $c = Get-Content -Raw $p; $c = $c -replace '(?m)^([ \t]*Standalone:[ \t]*)1[ \t]*$', '${1}0'; Set-Content -Path $p -Value $c -Encoding UTF8 }"
if errorlevel 1 (
    echo ERREUR : impossible de configurer le backend Mono Unity.
    goto :echec
)
for /d %%D in ("%PROJECT%\Library\PackageCache\com.unity.collab-proxy@*") do if exist "%%~fD" (
    echo Nettoyage de l'ancien paquet Unity Collab incompatible avec Unity 6.6...
    rmdir /s /q "%PROJECT%\Library"
    if exist "%PROJECT%\Packages\packages-lock.json" del /q "%PROJECT%\Packages\packages-lock.json"
)

rem On supprime UNIQUEMENT l'ancien export du jeu : le dossier jeu\ contient
rem aussi le launcher, le manifeste et LIS-MOI, qui ne doivent jamais partir.
rem Export dans un dossier temporaire : un ancien jeu verrouille ne doit pas
rem empecher Unity de compiler les nouveaux fichiers.
set "EXPORT=%BUILD%\unity_export"
if exist "%EXPORT%" rmdir /s /q "%EXPORT%"
if exist "%EXPORT%" (
    echo ERREUR : le dossier temporaire d'export est encore verrouille : %EXPORT%
    goto :echec
)
if exist "%BUILD%\launcher" rmdir /s /q "%BUILD%\launcher"
if exist "%BUILD%\unity.log" del /q "%BUILD%\unity.log"
mkdir "%EXPORT%"
mkdir "%BUILD%\launcher"

echo Creation de l'icone du launcher...
rem PyInstaller resout un chemin d'icone relatif depuis le dossier du .spec
rem (build\), donc l'icone doit lui etre donnee en chemin ABSOLU et entre
rem guillemets : le dossier du projet peut contenir des espaces.
set "ICONE_ARG=--icon="%ROOT%icon.ico""
if not exist "%ROOT%icon.ico" "%PYTHON%" "%ROOT%outils\creer_icone.py" "%LAUNCHER%" "%ROOT%icon.ico"
if not exist "%ROOT%icon.ico" set "ICONE_ARG="
if not defined ICONE_ARG echo AVERTISSEMENT : icone indisponible, compilation sans icone.

"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod LibreViesBuild.BuildWindows -buildPath "%EXPORT%\LibreViesGame.exe" -logFile "%BUILD%\unity.log"
if errorlevel 1 (
    echo ERREUR : export Unity echoue. Consultez build\unity.log
    goto :echec
)
if not exist "%EXPORT%\LibreViesGame.exe" (
    echo ERREUR : Unity n'a pas produit LibreViesGame.exe
    goto :echec
)
rem Remplacement seulement apres un export complet reussi. Si le jeu actuel
rem tourne encore, on arrete proprement ici avec un message explicite.
if exist "%JEU%\game" rmdir /s /q "%JEU%\game"
if exist "%JEU%\game" (
    echo ERREUR : impossible de remplacer jeu\game : fermez LibreViesGame.exe
    echo puis relancez build_launcher.bat. Le nouvel export reste dans : %EXPORT%
    goto :echec
)
move /Y "%EXPORT%" "%JEU%\game" >nul
if errorlevel 1 (
    echo ERREUR : impossible de mettre en place le nouvel export dans jeu\game.
    goto :echec
)
if not exist "%JEU%\game\LibreViesGame.exe" (
    echo ERREUR : le nouvel export a ete deplace mais son executable manque.
    goto :echec
)

echo.
echo [6/6] Compilation de l'amorce du launcher...
rem LibreVies.exe n'est plus qu'une AMORCE : elle contient le minimum
rem (runtime Python + tkinter) et execute le fichier launcher.pyw place a
rem cote d'elle. Consequences :
rem   * tout le launcher est dans launcher.pyw, qui se met a jour tout seul
rem     par son hash (version_url.json) : plus besoin de reconstruire
rem     l'executable quand le launcher change ;
rem   * cette compilation n'est necessaire que si l'amorce elle-meme change.
rem --add-data embarque launcher.pyw : s'il manque a cote de l'executable
rem (premier lancement), l'amorce le recopie.
if not exist "%AMORCE%" (
    echo ERREUR : l'amorce est introuvable : %AMORCE%
    echo Elle doit venir de compilation\amorce\amorce_launcher.py.
    goto :echec
)
rem Les modules exclus ne sont pas utilises par le launcher : sans cela,
rem PyInstaller embarque pygame/numpy s'ils sont installes sur la machine
rem de build, ce qui gonfle LibreVies.exe pour rien.
rem L'amorce ne change presque jamais : ne relance pas PyInstaller pour
rem chaque virgule du jeu. Le hash est conserve dans build\amorce.sha256.
set "LV_AMORCE=%AMORCE%"
set "LV_AMORCE_HASH=%BUILD%\amorce.sha256"
set "RECOMPILER_AMORCE=non"
if not exist "%JEU%\LibreVies.exe" set "RECOMPILER_AMORCE=oui"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $h=(Get-FileHash -LiteralPath $env:LV_AMORCE -Algorithm SHA256).Hash; $f=$env:LV_AMORCE_HASH; $ancien=''; if(Test-Path $f){$ancien=(Get-Content -Raw $f).Trim()}; Set-Content -Path $f -Value $h -Encoding ASCII; if($ancien -ne $h){exit 1}else{exit 0}"
if errorlevel 1 set "RECOMPILER_AMORCE=oui"
if "%RECOMPILER_AMORCE%"=="oui" (
    if exist "%BUILD%\LibreVies.spec" del /q "%BUILD%\LibreVies.spec"
    "%PYTHON%" -m PyInstaller --onefile --noconsole --clean --name LibreVies %ICONE_ARG% --add-data "%LAUNCHER%;." --exclude-module pygame --exclude-module numpy --exclude-module psutil --exclude-module setuptools --exclude-module pip --distpath "%JEU%" --workpath "%BUILD%\launcher" --specpath "%BUILD%" "%AMORCE%"
    if errorlevel 1 (
        echo ERREUR : compilation du launcher echouee.
        goto :echec
    )
    if not exist "%JEU%\LibreVies.exe" (
        echo ERREUR : l'executable du launcher est absent.
        goto :echec
    )
) else (
    echo        Amorce inchangee : PyInstaller ignore, executable conserve.
)

if exist "%BUILD%\launcher" rmdir /s /q "%BUILD%\launcher"
if exist "%ROOT%icon.ico" del /q "%ROOT%icon.ico"
if exist "%BUILD%\LibreVies.spec" del /q "%BUILD%\LibreVies.spec"

echo.
echo ==========================================================================
echo   TERMINE
echo ==========================================================================
echo   A donner au joueur : %JEU%\LibreVies.exe
echo   A publier (le jeu) : %JEU%\game\
echo.
echo   Le joueur ne peut jouer qu'apres la publication de cette compilation.
echo.
choice /c ON /n /m "Publier cette compilation maintenant pour tous les joueurs ? [O/N] "
if errorlevel 2 goto :fin
if not errorlevel 1 goto :fin
if exist "%ROOT%outils\publier_jeu.bat" (
    call "%ROOT%outils\publier_jeu.bat"
) else (
    echo ERREUR : %ROOT%outils\publier_jeu.bat est introuvable.
)
goto :fin

:echec
echo.
echo ==========================================================================
echo   ECHEC - rien n'a ete supprime : relance le script apres avoir lu les
echo   messages ci-dessus. Les telechargements deja faits ne sont pas repetes.
echo ==========================================================================
pause
exit /b 1

:fin
echo.
pause
exit /b 0


rem ==========================================================================
rem  MISE A JOUR DU SCRIPT LUI-MEME (aucune action manuelle)
rem  Un .bat ne peut pas se remplacer pendant qu'il tourne : on passe par un
rem  petit intermediaire qui attend la fin, copie la nouvelle version, efface
rem  le .maj puis relance la compilation avec le script neuf.
rem ==========================================================================
:appliquer_maj
set "LV_RELANCE=%BUILD%\maj_launcher.bat"
> "%LV_RELANCE%" echo @echo off
>> "%LV_RELANCE%" echo rem Intermediaire : applique build_launcher.bat.maj puis relance.
>> "%LV_RELANCE%" echo :attente
>> "%LV_RELANCE%" echo timeout /t 1 /nobreak ^>nul
>> "%LV_RELANCE%" echo copy /Y "%ROOT%build_launcher.bat.maj" "%ROOT%build_launcher.bat" ^>nul 2^>^&1
>> "%LV_RELANCE%" echo if errorlevel 1 goto attente
>> "%LV_RELANCE%" echo del /f /q "%ROOT%build_launcher.bat.maj" ^>nul 2^>^&1
>> "%LV_RELANCE%" echo start "" "%ROOT%build_launcher.bat"
>> "%LV_RELANCE%" echo del /f /q "%%~f0" ^>nul 2^>^&1
start "" /min cmd.exe /d /c "%LV_RELANCE%"
exit /b 0


rem ==========================================================================
rem  ETAPE 1 - le projet (sources) : MISE A JOUR depuis GitHub
rem  Les fichiers sont mis a jour a chaque lancement (le depot est la
rem  reference). Avant tout remplacement, l'ancienne version est copiee dans
rem  build\sauvegarde_locale\ : rien n'est jamais perdu.
rem ==========================================================================
:etape_projet
echo [1/6] Projet : mise a jour depuis GitHub...
echo        https://github.com/%DEPOT% ^(branche %BRANCHE%^)
set "LV_URL=https://github.com/%DEPOT%/archive/refs/heads/%BRANCHE%.zip"
set "LV_ZIP=%TEMP%\librevies-projet.zip"
set "LV_DIR=%TEMP%\librevies-projet"
set "LV_SAUVE=%BUILD%\sauvegarde_locale"

rem --- Copie de sauvegarde de ce qui peut etre remplace ---------------------
rem     (uniquement les sources : le dossier image\ fait 72 Mo et n'est
rem     jamais modifie par ce script, inutile de le recopier)
mkdir "%BUILD%" 2>nul
if exist "%LV_SAUVE%" rmdir /s /q "%LV_SAUVE%"
mkdir "%LV_SAUVE%" 2>nul
robocopy "%ROOT%unity" "%LV_SAUVE%\unity" /E /XD Library Temp Logs obj Build build .vs /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
robocopy "%ROOT%outils" "%LV_SAUVE%\outils" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
robocopy "%ROOT%amorce" "%LV_SAUVE%\amorce" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
robocopy "%JEU%" "%LV_SAUVE%\jeu" /E /XF *.exe /XD game game.ancien game.install /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
for %%F in (build_launcher.bat build_unity_game.bat setup_unity_build_tools.bat) do if exist "%ROOT%%%F" copy /y "%ROOT%%%F" "%LV_SAUVE%\%%F" >nul 2>&1
echo        Sauvegarde de l'ancienne version : build\sauvegarde_locale

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%outils\synchroniser_sources.ps1"

if errorlevel 1 (
    echo.
    echo ERREUR : impossible de recuperer le projet depuis GitHub.
    echo Verifie la connexion Internet ^(ou l'acces a github.com^) puis relance.
    exit /b 1
)
echo        Projet a jour.

if exist "%ROOT%build\script_recu.txt" (
    del /q "%ROOT%build\script_recu.txt" >nul 2>&1
    if exist "%ROOT%build_launcher.bat.maj" (
        echo.
        echo Une nouvelle version de build_launcher.bat a ete telechargee :
        echo elle s'installe toute seule et la compilation redemarre.
        echo Rien a renommer, rien a remplacer a la main.
        echo.
        call :appliquer_maj
        exit /b 0
    )
)

if not exist "%PROJECT%\Assets" (
    echo ERREUR : le projet Unity reste introuvable : %PROJECT%
    exit /b 1
)
if not exist "%LAUNCHER%" (
    echo ERREUR : le launcher reste introuvable : %LAUNCHER%
    exit /b 1
)
exit /b 0

rem ==========================================================================
rem  ETAPE 2 - Python
rem ==========================================================================
:etape_python
call :detecter_python
if defined PYTHON (
    echo [2/6] Python : deja installe ^(%PYTHON%^)
    exit /b 0
)

echo [2/6] Python absent : installation automatique...
where winget >nul 2>&1
if not errorlevel 1 (
    echo        installation via winget...
    winget install --exact --id Python.Python.3.12 --scope user --silent --accept-package-agreements --accept-source-agreements
)
call :detecter_python
if not defined PYTHON (
    echo        installation depuis python.org...
    call :installer_python_site
    call :detecter_python
)
if not defined PYTHON (
    echo        attente de la fin de l'installation...
    for /l %%I in (1,1,18) do (
        if not defined PYTHON timeout /t 5 /nobreak >nul
        if not defined PYTHON call :detecter_python
    )
)
if not defined PYTHON (
    echo.
    echo ERREUR : Python n'a pas pu etre installe automatiquement.
    echo Installe-le a la main depuis https://www.python.org/downloads/
    echo en cochant "Add python.exe to PATH", puis relance ce script.
    exit /b 1
)
echo        Python pret : %PYTHON%
exit /b 0

:detecter_python
set "PYTHON="
for /f "delims=" %%P in ('python -c "import sys;print(sys.executable)" 2^>nul') do set "PYTHON=%%P"
if not defined PYTHON for /f "delims=" %%P in ('py -3 -c "import sys;print(sys.executable)" 2^>nul') do set "PYTHON=%%P"
if not defined PYTHON for /d %%D in ("%LOCALAPPDATA%\Programs\Python\Python3*") do if exist "%%~fD\python.exe" set "PYTHON=%%~fD\python.exe"
if not defined PYTHON for /d %%D in ("%ProgramFiles%\Python3*") do if exist "%%~fD\python.exe" set "PYTHON=%%~fD\python.exe"
exit /b 0

:installer_python_site
set "LV_URL=https://www.python.org/ftp/python/3.12.7/python-3.12.7-amd64.exe"
call :lancer_installateur_python
if not errorlevel 1 exit /b 0
set "LV_URL=https://www.python.org/ftp/python/3.11.9/python-3.11.9-amd64.exe"
call :lancer_installateur_python
if not errorlevel 1 exit /b 0
exit /b 1

:lancer_installateur_python
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $f=Join-Path $env:TEMP 'librevies-python.exe'; try { Invoke-WebRequest -Uri $env:LV_URL -OutFile $f -UseBasicParsing } catch { Write-Host '        telechargement impossible'; exit 2 }; Write-Host '        installation silencieuse (2 a 3 minutes)...'; Start-Process -FilePath $f -ArgumentList '/quiet','InstallAllUsers=0','PrependPath=1','Include_launcher=1','Include_tcltk=1','Include_test=0' -Wait; Remove-Item -Force $f -ErrorAction SilentlyContinue; exit 0"
exit /b %errorlevel%


rem ==========================================================================
rem  ETAPE 3 - PyInstaller (fabrique LibreVies.exe)
rem ==========================================================================
:etape_pyinstaller
"%PYTHON%" -c "import PyInstaller" >nul 2>&1
if not errorlevel 1 (
    echo [3/6] PyInstaller : deja installe.
    exit /b 0
)
echo [3/6] PyInstaller absent : installation automatique...
"%PYTHON%" -m pip install --upgrade --disable-pip-version-check pyinstaller
if errorlevel 1 "%PYTHON%" -m pip install --user --upgrade --disable-pip-version-check pyinstaller
"%PYTHON%" -c "import PyInstaller" >nul 2>&1
if errorlevel 1 (
    echo.
    echo ERREUR : PyInstaller n'a pas pu etre installe.
    echo Essaie a la main :  "%PYTHON%" -m pip install pyinstaller
    exit /b 1
)
echo        PyInstaller pret.
exit /b 0


rem ==========================================================================
rem  ETAPE 4 - Unity (Hub + Editor) : telechargement automatique si absent
rem ==========================================================================
:etape_unity
set "UNITY=%LIBREVIES_UNITY%"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles(x86)%\Unity\Editor\Unity.exe" set "UNITY=%ProgramFiles(x86)%\Unity\Editor\Unity.exe"
if not defined UNITY for /r "%ProgramFiles%\Unity\Hub\Editor" %%F in (Unity.exe) do if not defined UNITY set "UNITY=%%F"
if not defined UNITY (
    echo [4/6] Unity absent : installation automatique ^(gros telechargement, une fois^)...
    call "%ROOT%setup_unity_build_tools.bat"
    set "UNITY=%LIBREVIES_UNITY%"
)
if not defined UNITY (
    echo.
    echo ERREUR : Unity Editor introuvable apres l'installation.
    echo Ouvre Unity Hub, connecte-toi et termine l'installation de 6000.6.1f1,
    echo puis relance ce script.
    exit /b 1
)
if not exist "%UNITY%" (
    echo ERREUR : UNITY pointe vers un fichier inexistant : %UNITY%
    exit /b 1
)
echo [4/6] Unity : %UNITY%
exit /b 0
