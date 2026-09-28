@echo off
setlocal EnableExtensions
rem ============================================================================
rem LibreVies - fabrication Unreal Windows
rem
rem Les sources Unreal et les caches restent dans compilation\unreal\.
rem La distribution joueur est placee dans jeu\game\.
rem Le createur de personnage est maintenant integre au meme executable Unreal.
rem ============================================================================
set "ROOT=%~dp0"
set "PROJECT=%ROOT%unreal\LibreVies.uproject"
set "BUILD=%ROOT%build"
set "EXPORT=%BUILD%\unreal_export"
set "JEU=%ROOT%..\jeu"
set "DEST=%JEU%\game"
set "UAT="

if not exist "%PROJECT%" (
    echo ERREUR : projet Unreal absent : %PROJECT%
    exit /b 1
)

rem LIBREVIES_UNREAL peut pointer vers le dossier UE_5.6 ou vers Engine.
set "UE=%LIBREVIES_UNREAL%"
if defined UE if exist "%UE%\Engine\Build\BatchFiles\RunUAT.bat" set "UAT=%UE%\Engine\Build\BatchFiles\RunUAT.bat"
if defined UE if exist "%UE%\Build\BatchFiles\RunUAT.bat" set "UAT=%UE%\Build\BatchFiles\RunUAT.bat"
for %%V in (5.6 5.5 5.4) do if not defined UAT if exist "%ProgramFiles%\Epic Games\UE_%%V\Engine\Build\BatchFiles\RunUAT.bat" set "UAT=%ProgramFiles%\Epic Games\UE_%%V\Engine\Build\BatchFiles\RunUAT.bat"
if not defined UAT (
    echo ERREUR : Unreal Engine 5.6 est introuvable.
    echo Installe Unreal Engine avec Epic Games Launcher, puis relance ce script.
    echo Tu peux aussi definir LIBREVIES_UNREAL vers le dossier UE_5.6.
    pause
    exit /b 1
)

if not exist "%BUILD%" mkdir "%BUILD%"
if exist "%EXPORT%" rmdir /s /q "%EXPORT%"
if exist "%EXPORT%" (
    echo ERREUR : export temporaire verrouille : %EXPORT%
    pause
    exit /b 1
)
mkdir "%EXPORT%"

echo ============================================================
echo   LibreVies - compilation Unreal Engine
echo ============================================================
echo Projet : %PROJECT%
echo UAT    : %UAT%
echo.
echo Le createur MakeHuman et l'atelier objets sont inclus dans le jeu.
echo.

call "%UAT%" BuildCookRun -project="%PROJECT%" -noP4 -utf8output -platform=Win64 -clientconfig=Shipping -build -cook -stage -pak -prereqs -archive -archivedirectory="%EXPORT%" -map="/Engine/Maps/Entry"
if errorlevel 1 (
    echo.
    echo ERREUR : compilation Unreal echouee.
    echo Consulte les journaux dans Saved\Logs et compilation\build\.
    pause
    exit /b 1
)

if not exist "%EXPORT%" (
    echo ERREUR : Unreal n'a produit aucune distribution.
    pause
    exit /b 1
)
if exist "%DEST%" rmdir /s /q "%DEST%"
if exist "%DEST%" (
    echo ERREUR : ferme le jeu Unreal avant de remplacer jeu\game.
    pause
    exit /b 1
)
mkdir "%DEST%"
robocopy "%EXPORT%" "%DEST%" /E /MOVE /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo ERREUR : impossible de copier la distribution dans jeu\game.
    pause
    exit /b 1
)

if not exist "%DEST%\LibreVies.exe" (
    echo AVERTISSEMENT : l'executable porte un autre nom. Cherche les .exe dans :
    dir /s /b "%DEST%\*.exe"
)
echo.
echo TERMINE
echo Distribution joueur : %DEST%
echo Le joueur ne recoit ni Unreal Engine ni Epic Games Launcher.
echo Conserve tout le dossier jeu\game\.
echo.
pause
exit /b 0
