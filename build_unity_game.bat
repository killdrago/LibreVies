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
set "PROJECT=%ROOT%unity"
if exist "%ROOT%Assets\Scripts" set "PROJECT=%ROOT%"
if exist "%ROOT%unity\unity\Assets\Scripts" set "PROJECT=%ROOT%unity\unity"
for /d %%D in ("%PROJECT%\Library\PackageCache\com.unity.collab-proxy@*") do if exist "%%~fD" (
    echo Nettoyage de l'ancien package Unity Collab incompatible avec Unity 6.6...
    rmdir /s /q "%PROJECT%\Library"
    if exist "%PROJECT%\Packages\packages-lock.json" del /q "%PROJECT%\Packages\packages-lock.json"
)

echo ========================================
echo   LibreVies - fabrication du jeu Unity
echo ========================================
echo.
echo Ce script est destine a la machine qui fabrique la distribution.
echo Le joueur n'aura pas besoin d'installer Unity.
echo.

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
    echo ERREUR : executable Unity introuvable : %UNITY%
    pause
    exit /b 1
)

if not exist "release\game" mkdir "release\game"
if exist "build\unity.log" del /q "build\unity.log"
if not exist "build" mkdir "build"

echo Export Windows Unity en cours...
"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod LibreViesBuild.BuildWindows -buildPath "%ROOT%release\game\LibreViesGame.exe" -logFile "%ROOT%build\unity.log"
if errorlevel 1 (
    echo ERREUR : Unity a echoue. Consultez build\unity.log
    pause
    exit /b 1
)
if not exist "release\game\LibreViesGame.exe" (
    echo ERREUR : LibreViesGame.exe n'a pas ete cree.
    pause
    exit /b 1
)

echo.
echo Jeu cree dans release\game\
echo IMPORTANT : Unity a aussi genere UnityPlayer.dll et un dossier *_Data.
echo Conservez tout le dossier game, pas seulement le .exe.
echo.
pause
