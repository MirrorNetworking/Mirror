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
set UNITY_NETWORKING="%UNITY_BASE_DIR%"\Editor\Data\UnityExtensions\Unity\Networking
set UNITY_MANAGED="%UNITY_BASE_DIR%"\Editor\Data\Managed

:: check the installation folder
if not exist "%UNITY_BASE_DIR%" (
  echo "Couldn't find Unity, is it even installed?"
  pause
  exit /B 1
)

:: 
if not exist %UNITY_NETWORKING% (
  echo "Couldn't find Unity's Network Folder, is this a broken Editor installation?"
  pause
  exit /B 1
)

if not exist %UNITY_MANAGED% (
  echo "Couldn't find Unity's Managed Data Folder, is this a broken Editor installation?"
  pause
  exit /B 1
)

:: Backup original files so that we can restore them later
if not exist "%UNITY_NETWORKING%"\UnityEngine.Networking.dll.orig (
  echo Creating backup of original HLAPI
  copy "%UNITY_NETWORKING%"\UnityEngine.Networking.dll  "%UNITY_NETWORKING%"\UnityEngine.Networking.dll.orig
  copy "%UNITY_NETWORKING%"\Editor\UnityEditor.Networking.dll  "%UNITY_NETWORKING%"\Editor\UnityEditor.Networking.dll.orig
  copy "%UNITY_NETWORKING%"\Standalone\UnityEngine.Networking.dll  "%UNITY_NETWORKING%"\Standalone\UnityEngine.Networking.dll.orig
  copy "%UNITY_MANAGED%"\Unity.UnetWeaver.dll  "%UNITY_MANAGED%"\Unity.UnetWeaver.dll.orig
)

:: Install files in unity folder
echo Okay, installing into %UNITY_BASE_DIR%.
echo Sit tight - if you get access denied errors, please run this again as Administrator.
echo Copying base DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\UnityEngine.Networking.* "%UNITY_NETWORKING%"
echo Copying Editor DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\Editor\*.* "%UNITY_NETWORKING%"\Editor
echo Copying Standalone DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\Standalone\*.* "%UNITY_NETWORKING%"\Standalone
echo Copying Weaver DLLs
copy /v /y %~dp0\Unity-Technologies-networking\Output\Weaver\*.* "%UNITY_MANAGED%"
echo If there are no errors, installation is complete. Otherwise, please check the base directory you entered.
echo Thank you for choosing HLAPI Community Edition. Happy developing!
pause
