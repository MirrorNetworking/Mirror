@echo off
title Unity HLAPI Community Edition Installer

:: Make sure we're in the right folder even in Administrator mode.
:: Admin mode starts off in %windir%\system32 for some reason
cd %~dp0
echo Installer started from %~dp0

:: Where oh where are you Unity?
if exist "C:\Program Files\Unity" (
  :: Unity 32bit on x86 OS, or Unity 64bit on x64 OS.
  set UNITY_BASE_DIR="C:\Program Files\Unity"
) else if exist "C:\Program Files (x86)\Unity" (
  :: Unity 32bit on x64 OS.
  set UNITY_BASE_DIR="C:\Program Files (x86)\Unity"
)

:: ask for unity base dir,  but default to the detected one
set /p UNITY_BASE_DIR="Enter Unity installation path or press [ENTER] for default [%UNITY_BASE_DIR%]: " 

if not exist %UNITY_BASE_DIR% (
  echo "Could not find unity"
  pause
  exit /B 1
)

:: Install files in unity folder
echo Okay, installing into %UNITY_BASE_DIR%.
echo Sit tight - if you get access denied errors, please run this again as Administrator.
echo Copying base DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\UnityEngine.Networking.* "%UNITY_BASE_DIR%"\Editor\Data\UnityExtensions\Unity\Networking
echo Copying Editor DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\Editor\*.* "%UNITY_BASE_DIR%"\Editor\Data\UnityExtensions\Unity\Networking\Editor
echo Copying Standalone DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\Standalone\*.* "%UNITY_BASE_DIR%"\Editor\Data\UnityExtensions\Unity\Networking\Standalone
echo Copying Weaver DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\Weaver\*.* "%UNITY_BASE_DIR%"\Editor\Data\Managed
echo If there are no errors, installation is complete. Otherwise, please check the base directory you entered.
pause
