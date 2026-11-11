@echo off
setlocal
title Bhorti Desk
cd /d "%~dp0"

echo ==========================================
echo   BHORTI DESK
echo ==========================================
echo.

where docker >nul 2>&1
if errorlevel 1 (
  if exist "%ProgramFiles%\Docker\Docker\resources\bin\docker.exe" set "PATH=%ProgramFiles%\Docker\Docker\resources\bin;%PATH%"
)
where docker >nul 2>&1
if errorlevel 1 (
  echo [X] Docker was not found.
  echo     Install Docker Desktop from docker.com, start it once,
  echo     then run this file again.
  echo.
  pause
  exit /b 1
)

echo [1/4] Checking that Docker is running ...
docker info >nul 2>&1
if errorlevel 1 (
  echo       starting Docker Desktop, this can take a minute ...
  if exist "%ProgramFiles%\Docker\Docker\Docker Desktop.exe" start "" "%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
  for /l %%i in (1,1,60) do (
    docker info >nul 2>&1 && goto docker_ok
    timeout /t 3 /nobreak >nul
  )
  echo.
  echo [X] Docker did not start. Open Docker Desktop by hand, wait until
  echo     it says "Engine running", then run this file again.
  pause
  exit /b 1
)
:docker_ok

echo [2/4] Building and starting the containers ...
echo       The first run downloads and builds images. It can take 10 minutes.
docker compose up -d --build
if errorlevel 1 (
  echo.
  echo [X] The containers did not start. See the error above.
  pause
  exit /b 1
)

echo [3/4] Waiting for the API ...
for /l %%i in (1,1,60) do (
  curl -s -o nul http://localhost:8080/health && goto api_ok
  timeout /t 3 /nobreak >nul
)
echo [X] The API did not answer. Run "docker compose logs backend" to see why.
pause
exit /b 1
:api_ok

echo [4/4] Ready.
echo.
echo   Open      http://localhost:3000/
echo   API       http://localhost:8080/classes
echo.
echo   The database starts empty. Open "School office" and create a class first.
echo   Stop it   docker compose down
echo.

start "" http://localhost:3000/
pause
