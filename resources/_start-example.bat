@echo off
cls
:start
echo Starting server...

Hurtworld.exe -batchmode -nographics -exec "host 12871;queryport 12881;maxplayers 60;servername My uMod Server"

echo.
echo Restarting server...
timeout /t 10
echo.
goto start
