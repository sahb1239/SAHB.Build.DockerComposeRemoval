@echo off
cd ../Build
powershell -ExecutionPolicy Bypass -File Build.ps1 -target Publish
pause