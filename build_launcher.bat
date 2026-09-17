@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ========================================
echo   LibreVies - build de la distribution Unity
echo ========================================
echo.
echo Le dossier release contiendra uniquement :
echo   LibreVies.exe       = launcher autonome
echo   game\LibreViesGame.exe = jeu Unity exporte
echo   version_url.json    = manifeste des mises a jour
echo.

rem Unity est requis uniquement sur la machine qui fabrique la build.
rem Il n'est jamais installe ou telecharge chez le joueur.
set "UNITY=%LIBREVIES_UNITY%"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles(x86)%\Unity\Editor\Unity.exe" set "UNITY=%ProgramFiles(x86)%\Unity\Editor\Unity.exe"
if not defined UNITY (
    echo ERREUR : Unity Editor introuvable.
    echo Definissez LIBREVIES_UNITY avec le chemin de Unity.exe.
    echo Cette etape est reservee au build, elle n'est pas demandee au joueur.
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
    echo Il est necessaire uniquement pour fabriquer LibreVies.exe.
    pause
    exit /b 1
)

if exist "release" rmdir /s /q "release"
if exist "build\launcher" rmdir /s /q "build\launcher"
if exist "build\unity.log" del /q "build\unity.log"
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
echo [1/3] Export du jeu Unity avec son runtime integre...
"%UNITY%" -batchmode -nographics -quit -projectPath "%~dp0unity" -executeMethod LibreViesBuild.BuildWindows -buildPath "%~dp0release\game\LibreViesGame.exe" -logFile "%~dp0build\unity.log"
if errorlevel 1 (
    echo ERREUR : export Unity echoue. Consultez build\unity.log
    pause
    exit /b 1
)
if not exist "release\game\LibreViesGame.exe" (
    echo ERREUR : Unity n'a pas produit LibreViesGame.exe
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
echo IMPORTANT : publiez les deux exe et version_url.json.
echo Pour les mises a jour, LIBREVIES_ASSET_BASE_URL doit pointer
echo vers l'URL publique des fichiers de la distribution.
pause
