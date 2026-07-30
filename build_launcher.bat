@echo off
cd /d "%~dp0"
echo ========================================
echo   Compilation du launcher en .exe
echo ========================================
echo.

echo [1/3] Conversion de l'icone...
python -c "from PIL import Image; img = Image.open('pp_lv_3.png'); img.save('icon.ico', format='ICO', sizes=[(256,256),(128,128),(64,64),(32,32),(16,16)])"
echo    Icone creee.

echo [2/3] Installation de PyInstaller...
pip install pyinstaller Pillow
echo [3/3] Compilation...
python -m PyInstaller --onefile --noconsole --name LibreVies --icon=icon.ico launcher.pyw

echo [3/3] Nettoyage...
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
