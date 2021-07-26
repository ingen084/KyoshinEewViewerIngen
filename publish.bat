rd /S /Q tmp
dotnet publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r win10-x64 -c release -o tmp/single -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=false --self-contained false
del tmp\single\*.pdb
dotnet publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r win10-x64 -c release -o tmp/merged -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true
del tmp\merged\*.pdb

powershell -c "Compress-Archive -Path tmp/single/* -DestinationPath tmp/KyoshinEewViewer_ingen_single.zip"
powershell -c "Compress-Archive -Path tmp/merged/* -DestinationPath tmp/KyoshinEewViewer_ingen_win10x64_merged.zip"

pause