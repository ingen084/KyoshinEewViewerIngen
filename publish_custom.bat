dotnet publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r %1 -c release -o tmp/%2 -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained %3
del tmp\merged\*.pdb

powershell -c "Compress-Archive -Path tmp/%2/* -DestinationPath tmp/KyoshinEewViewer_ingen_%1_%2.zip"
