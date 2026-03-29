@echo off
chcp 65001 >nul
echo [Bond-Defense] Exporting config tables...
cd /d "%~dp0"
py -m gd_excelexporter.cli gen-all
if %errorlevel% neq 0 (
    echo [ERROR] Export failed! Check log.txt for details.
    pause
    exit /b 1
)
echo [OK] Export done. Copying to Resources/Config...
xcopy /E /Y /I dist ..\Resources\Config\ >nul
echo [OK] Resources updated!
echo.
echo Tables exported:
echo   hero.json     - Hero definitions
echo   synergy.json  - Synergy/Bond data
echo   wave.json     - Wave difficulty config
echo   enemy.json    - Enemy types
echo   shop.json     - Shop settings
echo.
pause
