@echo off
setlocal EnableExtensions
rem ============================================================
rem  Publie la compilation du jeu pour tous les joueurs.
rem
rem  A lancer APRES build_launcher.bat (qui a rempli release\game).
rem  Double-clic : version et notes sont demandees.
rem ============================================================
set "OUTILS=%~dp0"
set "RACINE=%OUTILS%..\.."
set "JEU=%RACINE%\compilation\release\game"
set "EXE=%RACINE%\compilation\release\LibreVies.exe"

if not exist "%JEU%" (
    echo ERREUR : %JEU% est absent.
    echo Lance d'abord compilation\build_launcher.bat.
    pause
    exit /b 1
)

set "VERSION=%~1"
if not defined VERSION set /p VERSION=Version du jeu a publier (ex. 0.5.0) :
set "NOTES=%~2"
if not defined NOTES set /p NOTES=Notes affichees dans le launcher (Entree = garder) :

set "ARGS=--jeu "%JEU%" --version "%VERSION%""
if defined NOTES set "ARGS=%ARGS% --notes "%NOTES%""
if exist "%EXE%" set "ARGS=%ARGS% --exe "%EXE%""

python "%OUTILS%publier_jeu.py" %ARGS% --pousser
if errorlevel 1 (
    echo.
    echo La publication a echoue. Rien n'est perdu : relance le script.
    pause
    exit /b 1
)
echo.
pause
