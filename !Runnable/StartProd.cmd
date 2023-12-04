xcopy ..\TradePort\bin\Release\net8.0 .\TradePort\prod /s /d /e /k /h /i /y
cd TradePort\prod
start /b TradePort.exe PROD