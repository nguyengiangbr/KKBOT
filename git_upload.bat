@echo off
cd /d "D:\0.Game Final\7.KKBOT"
echo [1] Check git...
git status >nul 2>&1 || (echo LOI: Khong phai git repo & pause & exit /b 1)
echo OK
git status --short
pause

echo [1] Check secret...
git diff --cached --name-only | findstr /i "password secret token key" >nul && (echo CANH BAO: Co secret! & pause & exit /b 1)
echo OK
pause

echo [2] Add & Commit...
git add .
git commit -m "chore: backup %date:~-4,4%%date:~3,2%%date:~0,2%"
pause

echo [3] Push...
git push origin main
pause
echo DONE
pause