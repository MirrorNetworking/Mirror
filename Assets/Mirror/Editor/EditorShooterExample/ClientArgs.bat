for /L %%a in (1,1,3) do (
start cmd /k Mirror.exe -mode client -frameRate 30 -port 7777 -ip 127.0.0.1
)