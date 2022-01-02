#!/bin/bash

~/.dotnet/dotnet restore -r $2 src/KyoshinEewViewer/KyoshinEewViewer.csproj
~/.dotnet/dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=$2  -p:Configuration=Release src/KyoshinEewViewer/KyoshinEewViewer.csproj -p:PublishDir=../../tmp/$2_$3 -p:PublishReadyToRun=false -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseAppHost=true
chmod +x tmp/$2_$3/KyoshinEewViewer.app/Contents/MacOS/KyoshinEewViewer
cd tmp/$2_$3; zip -r ../KyoshinEewViewer_ingen_$2_$3.zip KyoshinEewViewer.app
