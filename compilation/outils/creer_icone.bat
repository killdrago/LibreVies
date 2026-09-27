@echo off
setlocal EnableExtensions
rem ============================================================
rem  Recree l'icone du launcher (icon.ico) a partir de launcher.pyw.
rem  Utile pour verifier l'icone, ou si un outil en reclame une.
rem  build_launcher.bat le fait tout seul ; ce .bat sert a la main.
rem ============================================================
python "%~dp0creer_icone.py" "%~dp0..\..\jeu\launcher.pyw" "%~dp0..\icon.ico"
if errorlevel 1 (
    echo.
    echo L'icone n'a pas pu etre creee : lis le message ci-dessus.
    pause
    exit /b 1
)
echo.
pause
