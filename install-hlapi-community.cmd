@echo off
cd %~dp0

:: find out where unity is installed
if exists "C:\Program Files\Unity" (
  set UNITY_BASE_DIR="C:\Program Files\Unity"
)
else if exists "C:\Program Files (x86)\Unity" (
  set UNITY_BASE_DIR="C:\Program Files (x86)\Unity"
)
else (
  set /p UNITY_BASE_DIR="Please enter the location of your Unity Installation (do not include the Editor folder suffix): "
)

echo Okay, installing into %UNITY_BASE_DIR%. Sit tight - if you get access denied errors, please run this again as Administrator.
echo Copying base DLLs
copy /v /y .\Unity-Technologies-networking\Output\UnityEngine.Networking.* "%UNITY_BASE_DIR%\Editor\Data\UnityExtensions\Unity\Networking"
echo Copying Editor DLLs
copy /v /y .\Unity-Technologies-networking\Output\Editor\*.* "%UNITY_BASE_DIR%\Editor\Data\UnityExtensions\Unity\Networking\Editor"
echo Copying Standalone DLLs
copy /v /y .\Unity-Technologies-networking\Output\Standalone\*.* "%UNITY_BASE_DIR%\Editor\Data\UnityExtensions\Unity\Networking\Standalone"
echo Copying Weaver DLLs
copy /v /y .\Unity-Technologies-networking\Output\Weaver\*.* "%UNITY_BASE_DIR%\Editor\Data\Managed"
echo If there are no errors, installation is complete. Otherwise, please check the base directory you entered.
pause
exit
