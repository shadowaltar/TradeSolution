#ngrok config add-authtoken XXXXX
ps | findstr ngrok
cd c:\Apps\ngrok\
rm ngrok.log
#PROD
.\ngrok.exe http https://localhost:50493 --host-header="localhost:50493" &
#UAT
.\ngrok.exe http https://localhost:50715 --host-header="localhost:50715" &
#TEST
.\ngrok.exe http https://localhost:55325 --host-header="localhost:55325" &
#SIM
.\ngrok.exe http https://localhost:50287 --host-header="localhost:50287" & 
