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
echo Verification de Pillow...
python -c "from PIL import Image" 2>nul && goto pillow_ok
echo    Pillow non trouve. Installation...
pip install Pillow
goto build

:pillow_ok
echo    Pillow OK.

:build
echo.
echo Conversion de l'icone...
python -c "from PIL import Image; img = Image.open('pp_lv_3.png'); img.save('icon.ico', format='ICO', sizes=[(256,256),(128,128),(64,64),(32,32),(16,16)])"

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
