xcopy ..\TradePort\bin\Release\net8.0 .\TradePort\uat /s /d /e /k /h /i /y
cd TradePort\uat
start /b TradePort.exe UAT