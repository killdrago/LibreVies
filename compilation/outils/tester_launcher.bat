@echo off
setlocal EnableExtensions
rem ============================================================
rem  Recette du launcher : verifie le telechargement, le md5,
rem  l'installation, la reprise apres coupure et les archives piegees.
rem  Verifie aussi l'amorce (test_amorce.py) : c'est LibreVies.exe, le
rem  programme qui execute launcher.pyw.
rem  Aucun fichier du depot n'est modifie (tout se passe dans un dossier
rem  temporaire) et rien n'est envoye sur Internet.
rem ============================================================
python "%~dp0test_launcher.py"
if errorlevel 1 (
    echo.
    echo Des tests ont echoue : lis les lignes ECHEC ci-dessus.
    pause
    exit /b 1
)
echo.
python "%~dp0test_amorce.py"
if errorlevel 1 (
    echo.
    echo L'amorce a echoue : lis les lignes ECHEC ci-dessus.
    pause
    exit /b 1
)
echo.
pause
