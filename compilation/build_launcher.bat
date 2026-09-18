@echo off
setlocal EnableExtensions
rem ============================================================
rem  LibreVies - fabrication de la distribution
rem
rem  Ce script vit dans compilation\ : il n'est JAMAIS donne au joueur.
rem  A la fin :
rem    compilation\release\LibreVies.exe      = LE SEUL fichier a donner au joueur
rem    compilation\release\game\...           = le jeu exporte, a publier
rem
rem  Etape suivante : outils\publier_jeu.bat (met le jeu en ligne)
rem ============================================================
set "ROOT=%~dp0"
set "LAUNCHER=%ROOT%..\jeu\launcher.pyw"
set "RELEASE=%ROOT%release"
set "PROJECT=%ROOT%unity"

if not exist "%LAUNCHER%" (
    echo ERREUR : le launcher est absent : %LAUNCHER%
    echo Attendu : <depot>\jeu\launcher.pyw et <depot>\compilation\build_launcher.bat
    pause
    exit /b 1
)
if not exist "%PROJECT%\Assets" (
    echo ERREUR : projet Unity absent : %PROJECT%
    pause
    exit /b 1
)

rem Force le backend Mono meme si l'installation locale conserve un ancien reglage IL2CPP.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Join-Path $env:PROJECT 'ProjectSettings\ProjectSettings.asset'; if (Test-Path $p) { $c = Get-Content -Raw $p; $c = $c -replace '(?m)^([ \t]*Standalone:[ \t]*)1[ \t]*$', '${1}0'; Set-Content -Path $p -Value $c -Encoding UTF8 }"
if errorlevel 1 (
    echo ERREUR : impossible de configurer le backend Mono Unity.
    pause
    exit /b 1
)
for /d %%D in ("%PROJECT%\Library\PackageCache\com.unity.collab-proxy@*") do if exist "%%~fD" (
    echo Nettoyage de l'ancien package Unity Collab incompatible avec Unity 6.6...
    rmdir /s /q "%PROJECT%\Library"
    if exist "%PROJECT%\Packages\packages-lock.json" del /q "%PROJECT%\Packages\packages-lock.json"
)

echo ========================================
echo   LibreVies - build de la distribution
echo ========================================
echo.
echo Unity et Python ne sont necessaires QUE sur cette machine.
echo Le joueur, lui, ne recevra que LibreVies.exe.
echo.

set "UNITY=%LIBREVIES_UNITY%"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles(x86)%\Unity\Editor\Unity.exe" set "UNITY=%ProgramFiles(x86)%\Unity\Editor\Unity.exe"
if not defined UNITY for /r "%ProgramFiles%\Unity\Hub\Editor" %%F in (Unity.exe) do if not defined UNITY set "UNITY=%%F"
if not defined UNITY (
    echo Unity Editor absent : lancement de l'installation automatique...
    call "%ROOT%setup_unity_build_tools.bat"
    if errorlevel 1 (
        echo ERREUR : installation automatique de Unity impossible.
        pause
        exit /b 1
    )
    set "UNITY=%LIBREVIES_UNITY%"
)
if not defined UNITY (
    echo ERREUR : Unity Editor introuvable apres l'installation.
    pause
    exit /b 1
)
if not exist "%UNITY%" (
    echo ERREUR : UNITY pointe vers un fichier inexistant : %UNITY%
    pause
    exit /b 1
)

python -c "import PyInstaller" >nul 2>&1
if errorlevel 1 (
    echo ERREUR : PyInstaller manque sur la machine de build.
    echo Installe-le avec :  python -m pip install pyinstaller
    echo Il est necessaire uniquement pour fabriquer LibreVies.exe.
    pause
    exit /b 1
)

cd /d "%ROOT%"
if exist "%RELEASE%" rmdir /s /q "%RELEASE%"
if exist "%ROOT%build\launcher" rmdir /s /q "%ROOT%build\launcher"
if exist "%ROOT%build\unity.log" del /q "%ROOT%build\unity.log"
mkdir "%RELEASE%\game"
mkdir "%ROOT%build\launcher"

if not exist "%ROOT%icon.ico" (
    echo Creation de l'icone depuis le launcher...
    python -c "import importlib.util as u; s=u.spec_from_file_location('lv',r'%LAUNCHER%'); m=u.module_from_spec(s); s.loader.exec_module(m); m.export_icon(r'%ROOT%icon.ico')"
    if errorlevel 1 (
        echo ERREUR : impossible de creer icon.ico
        pause
        exit /b 1
    )
)

echo.
echo [1/3] Export du jeu Unity avec son runtime integre...
"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod LibreViesBuild.BuildWindows -buildPath "%RELEASE%\game\LibreViesGame.exe" -logFile "%ROOT%build\unity.log"
if errorlevel 1 (
    echo ERREUR : export Unity echoue. Consultez build\unity.log
    pause
    exit /b 1
)
if not exist "%RELEASE%\game\LibreViesGame.exe" (
    echo ERREUR : Unity n'a pas produit LibreViesGame.exe
    pause
    exit /b 1
)

echo.
echo [2/3] Compilation du launcher autonome (le seul fichier donne au joueur)...
python -m PyInstaller --onefile --noconsole --clean --name LibreVies --icon="%ROOT%icon.ico" --distpath "%RELEASE%" --workpath "%ROOT%build\launcher" --specpath "%ROOT%build" "%LAUNCHER%"
if errorlevel 1 (
    echo ERREUR : compilation du launcher echouee.
    pause
    exit /b 1
)
if not exist "%RELEASE%\LibreVies.exe" (
    echo ERREUR : l'executable du launcher est absent.
    pause
    exit /b 1
)

echo.
echo [3/3] Copie du manifeste joueur a cote du launcher (pour tester ici)...
copy /y "%ROOT%..\jeu\version_url.json" "%RELEASE%\version_url.json" >nul

if exist "%ROOT%build\launcher" rmdir /s /q "%ROOT%build\launcher"
if exist "%ROOT%icon.ico" del /q "%ROOT%icon.ico"
if exist "%ROOT%build\LibreVies.spec" del /q "%ROOT%build\LibreVies.spec"
if exist "%ROOT%LibreVies.spec" del /q "%ROOT%LibreVies.spec"

echo.
echo ========================================
echo   TERMINE
echo ========================================
echo   A donner au joueur  : %RELEASE%\LibreVies.exe
echo   A publier (le jeu)  : %RELEASE%\game\  (via outils\publier_jeu.bat)
echo.
echo Pour mettre le jeu en ligne pour tout le monde :
echo   compilation\outils\publier_jeu.bat
echo.
echo Pour tester ici sans publier : lance %RELEASE%\LibreVies.exe
echo.
pause
