dotnet publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r %2 -c release -o tmp/%2_%3 -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=%4 --self-contained %4
del tmp\%2_%3\*.pdb

powershell -c "Compress-Archive -Path tmp/%2_%3/* -DestinationPath tmp/KyoshinEewViewer_ingen_%2_%3.zip"
