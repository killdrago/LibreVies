@echo off
setlocal EnableExtensions
rem ============================================================
rem  LibreVies - build du createur humain autonome seul
rem
rem  Ce script est volontairement independant du build du jeu principal.
rem  Il place toujours le logiciel final dans jeu\personnage\ afin que le
rem  createur puisse etre reutilise plus tard au lancement du jeu.
rem ============================================================
set "ROOT=%~dp0"
set "PROJECT=%ROOT%personnage"
set "JEU=%ROOT%..\jeu"
set "PERSONNAGE=%JEU%\personnage"
set "BUILD=%ROOT%build"
set "EXPORT=%BUILD%\personnage_export"

if not exist "%PROJECT%\Assets" (
    echo ERREUR : projet personnage absent : %PROJECT%
    echo Lancez ce script depuis compilation\ ou lancez build_launcher.bat.
    pause
    exit /b 1
)

set "UNITY=%LIBREVIES_UNITY%"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe" set "UNITY=%ProgramFiles%\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe"
if not defined UNITY if exist "%ProgramFiles(x86)%\Unity\Editor\Unity.exe" set "UNITY=%ProgramFiles(x86)%\Unity\Editor\Unity.exe"
if not defined UNITY for /r "%ProgramFiles%\Unity\Hub\Editor" %%F in (Unity.exe) do if not defined UNITY set "UNITY=%%F"
if not defined UNITY (
    echo ERREUR : Unity 6000.6.1f1 est necessaire pour fabriquer le logiciel.
    echo Lancez d'abord compilation\build_launcher.bat pour l'installation automatique.
    pause
    exit /b 1
)
if not exist "%UNITY%" (
    echo ERREUR : Unity introuvable : %UNITY%
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
if exist "%BUILD%\personnage.log" del /q "%BUILD%\personnage.log"

rem Le projet contient toutes les ressources du logiciel. Unity ne sert ici
rem qu'a fabriquer l'executable Windows autonome.
echo ========================================
echo   LibreVies - createur humain 3D
echo ========================================
echo.
echo Projet source : %PROJECT%
echo Destination   : %PERSONNAGE%
echo.

"%UNITY%" -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod CharacterCreatorBuild.BuildWindows -buildPath "%EXPORT%\LibreViesPersonnage.exe" -logFile "%BUILD%\personnage.log"
if errorlevel 1 (
    echo ERREUR : Unity a echoue. Consultez : %BUILD%\personnage.log
    pause
    exit /b 1
)
if not exist "%EXPORT%\LibreViesPersonnage.exe" (
    echo ERREUR : LibreViesPersonnage.exe n'a pas ete cree.
    echo Consultez : %BUILD%\personnage.log
    pause
    exit /b 1
)

rem Les notices restent dans jeu\personnage. Seuls les anciens binaires sont
rem remplaces, jamais le README de destination.
for %%F in (LibreViesPersonnage.exe UnityPlayer.dll UnityCrashHandler64.exe) do if exist "%PERSONNAGE%\%%F" del /q "%PERSONNAGE%\%%F"
if exist "%PERSONNAGE%\LibreViesPersonnage_Data" rmdir /s /q "%PERSONNAGE%\LibreViesPersonnage_Data"
if exist "%PERSONNAGE%\MonoBleedingEdge" rmdir /s /q "%PERSONNAGE%\MonoBleedingEdge"
if not exist "%PERSONNAGE%" mkdir "%PERSONNAGE%"
robocopy "%EXPORT%" "%PERSONNAGE%" /E /MOVE /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo ERREUR : impossible de copier le logiciel dans jeu\personnage.
    pause
    exit /b 1
)
if not exist "%PERSONNAGE%\LibreViesPersonnage.exe" (
    echo ERREUR : le logiciel n'est pas present dans jeu\personnage.
    pause
    exit /b 1
)

echo.
echo TERMINE
echo Logiciel pret : %PERSONNAGE%\LibreViesPersonnage.exe
echo Le dossier complet jeu\personnage doit etre conserve.
echo.
pause
exit /b 0
