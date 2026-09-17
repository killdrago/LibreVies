@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ========================================
echo   LibreVies - build de la distribution
echo ========================================
echo.
echo Le dossier release contiendra uniquement :
echo   LibreVies.exe       = launcher autonome
echo   game\LibreViesGame.exe = jeu exporte avec son runtime Godot
echo   version_url.json    = manifeste des mises a jour
echo.

rem Le runtime Godot n'est JAMAIS telecharge par le joueur. Cette recherche
rem sert seulement a la machine qui fabrique la distribution.
set "GODOT=%LIBREVIES_GODOT%"
if not defined GODOT if exist "%~dp0tools\godot.exe" set "GODOT=%~dp0tools\godot.exe"
if not defined GODOT if exist "%~dp0tools\Godot_v4.7.2-stable_win64.exe" set "GODOT=%~dp0tools\Godot_v4.7.2-stable_win64.exe"
if not defined GODOT (
    for /r "%~dp0tools" %%G in (godot*.exe) do if not defined GODOT set "GODOT=%%G"
)
if not defined GODOT (
    echo ERREUR : Godot exportable introuvable.
    echo Placez l'editeur portable Godot dans tools\ ou definissez LIBREVIES_GODOT.
    echo Cette etape est reservee au build, elle n'est pas demandee au joueur.
    pause
    exit /b 1
)
if not exist "%GODOT%" (
    echo ERREUR : GODOT pointe vers un fichier inexistant : %GODOT%
    pause
    exit /b 1
)

python -c "import PyInstaller" >nul 2>&1
if errorlevel 1 (
    echo ERREUR : PyInstaller manque sur la machine de build.
    echo Installez-le une seule fois sur la machine du developpeur, pas chez les joueurs.
    pause
    exit /b 1
)

if exist "release" rmdir /s /q "release"
if exist "build\launcher" rmdir /s /q "build\launcher"
mkdir "release\game"
mkdir "build\launcher"

if not exist "icon.ico" (
    echo Creation de l'icone...
    python -c "import importlib.util as u; s=u.spec_from_file_location('lv','launcher.pyw'); m=u.module_from_spec(s); s.loader.exec_module(m); m.export_icon('icon.ico')"
    if errorlevel 1 (
        echo ERREUR : impossible de creer icon.ico
        pause
        exit /b 1
    )
)

echo.
echo [1/3] Export du jeu Godot avec runtime integre...
"%GODOT%" --headless --path "%~dp0" --export-release "Windows Desktop" "%~dp0release\game\LibreViesGame.exe"
if errorlevel 1 (
    echo ERREUR : export Godot echoue.
    pause
    exit /b 1
)
if not exist "release\game\LibreViesGame.exe" (
    echo ERREUR : l'export n'a pas produit LibreViesGame.exe
    pause
    exit /b 1
)

echo.
echo [2/3] Compilation du launcher autonome...
python -m PyInstaller --onefile --noconsole --clean --name LibreVies --icon=icon.ico --distpath "release" --workpath "build\launcher" --specpath "build" launcher.pyw
if errorlevel 1 (
    echo ERREUR : compilation du launcher echouee.
    pause
    exit /b 1
)
if not exist "release\LibreVies.exe" (
    echo ERREUR : l'executable du launcher est absent.
    pause
    exit /b 1
)

echo.
echo [3/3] Creation du manifeste de mise a jour...
if not defined LIBREVIES_ASSET_BASE_URL echo AVERTISSEMENT : LIBREVIES_ASSET_BASE_URL est vide ; publiez aussi les binaires a l'URL raw_url.
python make_package_manifest.py "release" --config version_url.json --output "release\version_url.json" --asset-base-url "%LIBREVIES_ASSET_BASE_URL%"
if errorlevel 1 (
    echo ERREUR : impossible de creer le manifeste.
    pause
    exit /b 1
)
copy /y "README_RELEASE_FR.md" "release\README.txt" >nul

if exist "build\launcher" rmdir /s /q "build\launcher"
if exist "icon.ico" del /q "icon.ico"
if exist "LibreVies.spec" del /q "LibreVies.spec"

echo.
echo ========================================
echo   TERMINE
echo   Distribution : %~dp0release\
echo   Lancez release\LibreVies.exe
echo ========================================
echo.
echo IMPORTANT : pour les MAJ du jeu, publiez les deux exe et
echo version_url.json, puis configurez LIBREVIES_ASSET_BASE_URL
echo vers l'URL publique de ces fichiers.
pause
