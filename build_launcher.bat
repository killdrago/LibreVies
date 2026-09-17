@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
if not exist "%ROOT%launcher.pyw" (
    if exist "%ROOT%..\launcher.pyw" for %%R in ("%ROOT%..") do set "ROOT=%%~fR\"
)
if not exist "%ROOT%launcher.pyw" (
    echo ERREUR : launcher.pyw est absent de la racine du projet : %ROOT%
    echo Placez ce script dans le depot LibreVies complet.
    pause
    exit /b 1
)
cd /d "%ROOT%"

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
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles(x86)%\Unity\Editor\Unity.exe" set "UNITY=%ProgramFiles(x86)%\Unity\Editor\Unity.exe"
if not defined UNITY for /r "%ProgramFiles%\Unity\Hub\Editor" %%F in (Unity.exe) do if not defined UNITY set "UNITY=%%F"
if not defined UNITY (
    echo Unity Editor absent : lancement de l'installation automatique...
    call "%~dp0setup_unity_build_tools.bat"
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
    echo Il est necessaire uniquement pour fabriquer LibreVies.exe.
    pause
    exit /b 1
)

if exist "release" rmdir /s /q "release"
if exist "build\launcher" rmdir /s /q "build\launcher"
if exist "build\unity.log" del /q "build\unity.log"
mkdir "release\game"
mkdir "build\launcher"

if not exist "%ROOT%icon.ico" (
    echo Creation de l'icone...
    python -c "import importlib.util as u; s=u.spec_from_file_location('lv',r'%ROOT%launcher.pyw'); m=u.module_from_spec(s); s.loader.exec_module(m); m.export_icon(r'%ROOT%icon.ico')"
    if errorlevel 1 (
        echo ERREUR : impossible de creer icon.ico
        pause
        exit /b 1
    )
)

echo.
echo [1/3] Export du jeu Unity avec son runtime integre...
"%UNITY%" -batchmode -nographics -quit -projectPath "%ROOT%unity" -executeMethod LibreViesBuild.BuildWindows -buildPath "%ROOT%release\game\LibreViesGame.exe" -logFile "%ROOT%build\unity.log"
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
python -m PyInstaller --onefile --noconsole --clean --name LibreVies --icon="%ROOT%icon.ico" --distpath "release" --workpath "build\launcher" --specpath "%ROOT%build" "%ROOT%launcher.pyw"
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
if exist "%ROOT%icon.ico" del /q "%ROOT%icon.ico"
if exist "LibreVies.spec" del /q "LibreVies.spec"

echo.
echo ========================================
echo   TERMINE
echo   Distribution : %ROOT%release\
echo   Lancez release\LibreVies.exe
echo ========================================
echo.
echo IMPORTANT : publiez LibreVies.exe, tout le dossier game et version_url.json.
echo Pour les mises a jour, LIBREVIES_ASSET_BASE_URL doit pointer
echo vers l'URL publique des fichiers de la distribution.
pause
