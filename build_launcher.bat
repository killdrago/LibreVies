@echo off
cd /d "%~dp0"
echo ========================================
echo   Compilation du launcher en .exe
echo ========================================
echo.

echo Verification de PyInstaller...
python -c "import PyInstaller" 2>nul && goto pyinstaller_ok
echo    PyInstaller non trouve. Installation...
pip install pyinstaller
goto compile

:pyinstaller_ok
echo    PyInstaller OK.

:compile
echo.
echo Extraction de l'icone depuis launcher.pyw (images integrees)...
python -c "import importlib.util as u; s=u.spec_from_file_location('lv','launcher.pyw'); m=u.module_from_spec(s); s.loader.exec_module(m); m.export_icon('icon.ico')"
if exist "icon.ico" (
    echo    icon.ico cree.
) else (
    echo    ERREUR : icon.ico n'a pas pu etre cree.
    pause
    exit /b 1
)

echo.
echo Compilation en .exe...
python -m PyInstaller --onefile --noconsole --name LibreVies --icon=icon.ico launcher.pyw

echo.
echo Nettoyage...
if exist "dist\LibreVies.exe" (
    copy "dist\LibreVies.exe" "LibreVies.exe"
    rmdir /s /q dist build
    del LibreVies.spec 2>nul
    echo.
    echo ========================================
    echo   TERMINE ! LibreVies.exe cree
    echo   Copiez-le sur votre bureau.
    echo ========================================
) else (
    echo ERREUR : Compilation echouee
)

pause
