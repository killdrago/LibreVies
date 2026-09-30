@echo off
cd /d "%~dp0"
python -m pip install -r requirements.txt
python atelier3d.py
pause
