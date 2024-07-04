for /L %%a in (1,1,1) do (
start cmd /k Mirror.exe -mode server -frameRate 120 -port 7777 -maxConnections 100
)