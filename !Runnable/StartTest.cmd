xcopy ..\TradePort\bin\Release\net8.0 .\TradePort\test /s /d /e /k /h /i /y
cd TradePort\test
start /b TradePort.exe TEST