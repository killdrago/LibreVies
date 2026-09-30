@echo off
cd /d "%~dp0"
python update_vetement.py
python -m pip install -r requirements.txt
python atelier3d.py
pause
