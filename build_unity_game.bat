@echo off
setlocal EnableExtensions
cd /d "%~dp0"

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
"%UNITY%" -batchmode -nographics -quit -projectPath "%~dp0unity" -executeMethod LibreViesBuild.BuildWindows -buildPath "%~dp0release\game\LibreViesGame.exe" -logFile "%~dp0build\unity.log"
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
