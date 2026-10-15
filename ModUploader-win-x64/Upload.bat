@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
ModUploader.exe upload -w BossSurvival
if errorlevel 1 (
  echo 上传失败，错误代码：%errorlevel%。
  pause
  exit /b %errorlevel%
)
echo 上传完成。
pause
