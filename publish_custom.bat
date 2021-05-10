dotnet publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r %1 -c release -o tmp/%1_%2 -p:PublishReadyToRun=%4 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained %3
del tmp\%1_%2\*.pdb

powershell -c "Compress-Archive -Path tmp/%1_%2/* -DestinationPath tmp/KyoshinEewViewer_ingen_%1_%2.zip"
