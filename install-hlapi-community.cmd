@echo off
title Unity HLAPI Community Edition Installer

:: Make sure we're in the right folder even in Administrator mode.
:: Admin mode starts off in %windir%\system32 for some reason
cd %~dp0
echo Installer started from %~dp0
:: Where oh where are you Unity?
if exist "C:\Program Files\Unity" (
  :: Unity 32bit on x86 OS, or Unity 64bit on x64 OS.
  echo Found a native Unity in your Program Files directory. Is this correct?
  CHOICE /C YN /M "Is this correct?"
  if errorlevel 2 goto promptForPath
  if errorlevel 1 goto prepUnityPath3264ForInstall 
  goto installDLLs
) else if exist "C:\Program Files (x86)\Unity" (
  :: Unity 32bit on x64 OS.
  echo Found Unity in your Program Files directory. This seems to be Unity 32-Bit on a x64 Operating System.
  CHOICE /C YN /M "Is this correct?"
  if errorlevel 2 goto promptForPath
  if errorlevel 1 goto prepUnityPath32ForInstall 
) else (
  :: Can't find it!
  goto promptForPath
)

:prepUnityPath3264ForInstall
set UNITY_BASE_DIR=C:\Program Files\Unity
goto installDLLs

:prepUnityPath32ForInstall
set UNITY_BASE_DIR=C:\Program Files (x86)\Unity
goto installDLLs

:installDLLs
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
exit

:promptForPath
echo We couldn't find the location of your Unity Install. Please enter the path below, but do not include the Editor folder suffix. A correct path would be "C:\UnityLTS2017.4", a wrong path would be "C:\UnityLTS2017.4\Editor".
set /p UNITY_BASE_DIR="Installation path? "
goto installDLLs