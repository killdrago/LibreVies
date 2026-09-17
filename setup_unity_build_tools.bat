@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Installation automatique des outils de fabrication, jamais des outils du joueur.
set "UNITY_VERSION=2022.3.62f1"
set "HUB=%ProgramFiles%\Unity Hub\Unity Hub.exe"
if not exist "%HUB%" if exist "%ProgramFiles(x86)%\Unity Hub\Unity Hub.exe" set "HUB=%ProgramFiles(x86)%\Unity Hub\Unity Hub.exe"

if not exist "%HUB%" (
    echo Unity Hub absent : telechargement depuis le site officiel Unity...
    set "HUB_INSTALLER=%TEMP%\UnityHubSetup.exe"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing -Uri 'https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe' -OutFile $env:HUB_INSTALLER"
    if errorlevel 1 (
        echo ERREUR : impossible de telecharger Unity Hub.
        exit /b 1
    )
    start /wait "" "%HUB_INSTALLER%" /S
    del /q "%HUB_INSTALLER%" 2>nul
    if exist "%ProgramFiles%\Unity Hub\Unity Hub.exe" set "HUB=%ProgramFiles%\Unity Hub\Unity Hub.exe"
    if not exist "%HUB%" if exist "%ProgramFiles(x86)%\Unity Hub\Unity Hub.exe" set "HUB=%ProgramFiles(x86)%\Unity Hub\Unity Hub.exe"
)

if not exist "%HUB%" (
    echo ERREUR : Unity Hub n'a pas ete installe.
    exit /b 1
)

set "UNITY=%ProgramFiles%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"
if not exist "%UNITY%" if exist "%ProgramFiles(x86)%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe" set "UNITY=%ProgramFiles(x86)%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"
if exist "%UNITY%" (
    endlocal & set "LIBREVIES_UNITY=%UNITY%" & exit /b 0
)

echo Unity %UNITY_VERSION% absent : installation automatique de l'Editor Windows...
echo Unity Hub peut demander une connexion a un compte Unity et l'activation d'une licence Personal.
echo Cette licence est necessaire uniquement pour fabriquer le jeu, jamais pour le joueur.

rem Les deux syntaxes couvrent les versions recentes de Unity Hub.
"%HUB%" --headless install --version %UNITY_VERSION% --module windows-mono --module windows-il2cpp --childModules
if errorlevel 1 "%HUB%" -- --headless install --version %UNITY_VERSION% --module windows-mono --module windows-il2cpp --childModules

for /r "%ProgramFiles%\Unity\Hub\Editor\%UNITY_VERSION%" %%F in (Unity.exe) do if not defined UNITY set "UNITY=%%F"
if not defined UNITY for /r "%ProgramFiles(x86)%\Unity\Hub\Editor\%UNITY_VERSION%" %%F in (Unity.exe) do if not defined UNITY set "UNITY=%%F"
if not defined UNITY (
    echo.
    echo Unity n'est pas encore disponible.
    echo Connectez-vous dans Unity Hub, terminez l'installation de %UNITY_VERSION%, puis relancez build_launcher.bat.
    exit /b 1
)

endlocal & set "LIBREVIES_UNITY=%UNITY%" & exit /b 0
