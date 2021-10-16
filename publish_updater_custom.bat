dotnet publish src/KyoshinEewViewer.Updater/KyoshinEewViewer.Updater.csproj -r %2 -c release -o tmp/%2_%3 -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=%4 --self-contained %4

powershell -c "Compress-Archive -Path tmp/%2_%3/* -DestinationPath tmp/KyoshinEewViewer_ingen_updater_%2_%3.zip"
