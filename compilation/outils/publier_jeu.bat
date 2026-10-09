@echo off
setlocal EnableExtensions
rem Le launcher lit jeucompiler : ne plus publier l'ancien manifeste jeu en archive.
set "RACINE=%~dp0..\.."
if not exist "%RACINE%\jeu\game\version_jeu.json" (
    echo ERREUR : export Unity verifie absent.
    echo Relance compilation\build_launcher.bat avant de publier.
    pause
    exit /b 1
)
if not exist "%RACINE%\publier_jeu_compiler.py" (
    echo ERREUR : publieur jeucompiler absent. Mets le projet a jour.
    pause
    exit /b 1
)
cd /d "%RACINE%"
python "%RACINE%\publier_jeu_compiler.py"
if errorlevel 1 (
    echo ERREUR : publication jeucompiler interrompue.
    pause
    exit /b 1
)
exit /b 0
