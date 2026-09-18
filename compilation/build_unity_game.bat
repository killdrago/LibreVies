@echo off
setlocal EnableExtensions
rem ============================================================
rem  LibreVies - export du jeu Unity seul (etape de diagnostic)
rem
rem  Le cas normal est de lancer build_launcher.bat, qui exporte le jeu
rem  ET fabrique le launcher. Ce script-ci ne sert qu'a verifier l'export
rem  Unity quand quelque chose ne va pas.
rem ============================================================
set "ROOT=%~dp0"
set "RELEASE=%ROOT%release"
set "PROJECT=%ROOT%unity"

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
echo   LibreVies - export du jeu Unity
echo ========================================
echo.
echo Script de diagnostic : le joueur n'a jamais besoin d'Unity.
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
    echo ERREUR : executable Unity introuvable : %UNITY%
    pause
    exit /b 1
)

if not exist "%RELEASE%\game" mkdir "%RELEASE%\game"
if not exist "%ROOT%build" mkdir "%ROOT%build"
if exist "%ROOT%build\unity.log" del /q "%ROOT%build\unity.log"

echo Export Windows Unity en cours...
"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod LibreViesBuild.BuildWindows -buildPath "%RELEASE%\game\LibreViesGame.exe" -logFile "%ROOT%build\unity.log"
if errorlevel 1 (
    echo ERREUR : Unity a echoue. Consultez build\unity.log
    pause
    exit /b 1
)
if not exist "%RELEASE%\game\LibreViesGame.exe" (
    echo ERREUR : LibreViesGame.exe n'a pas ete cree.
    pause
    exit /b 1
)

echo.
echo Jeu exporte dans %RELEASE%\game\
echo IMPORTANT : Unity a aussi genere UnityPlayer.dll et un dossier *_Data.
echo Conserve tout le dossier game, pas seulement le .exe.
echo.
echo Pour publier cette compilation : outils\publier_jeu.bat
echo.
pause
