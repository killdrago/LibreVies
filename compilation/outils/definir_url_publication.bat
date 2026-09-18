@echo off
setlocal EnableExtensions
rem ============================================================
rem  Indique au launcher OU lire les mises a jour.
rem
rem  Utilise ce script quand tu fusionnes le travail dans une autre branche
rem  (par exemple main) : il met a jour le manifeste ET la valeur de secours
rem  ecrite dans le launcher, puis verifie que le launcher compile encore.
rem ============================================================
set "BRANCHE=%~1"
if not defined BRANCHE set /p BRANCHE=Branche GitHub a publier (ex. main) :
if not defined BRANCHE (
    echo Aucune branche indiquee : rien n'a ete modifie.
    pause
    exit /b 1
)
python "%~dp0definir_url_publication.py" --branche "%BRANCHE%"
if errorlevel 1 (
    echo.
    echo Echec : rien n'a ete modifie.
    pause
    exit /b 1
)
echo.
pause
