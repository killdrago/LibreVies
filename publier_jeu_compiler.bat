@echo off
setlocal
rem Lance l'application graphique de publication du dossier jeu.
rem Elle synchronise vers « jeu compiler », decoupe les envois par lots
rem et pousse chaque lot sur la branche GitHub active.
cd /d "%~dp0"
where python >nul 2>&1
if errorlevel 1 (
    echo Python est introuvable. Installe Python 3 puis relance.
    pause
    exit /b 1
)
python "%~dp0publier_jeu_compiler.py"
if errorlevel 1 pause
