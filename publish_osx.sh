#!/bin/bash

~/.dotnet/dotnet restore -r osx-x64 src/KyoshinEewViewer/KyoshinEewViewer.csproj
~/.dotnet/dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=$2  -p:Configuration=Release src/KyoshinEewViewer/KyoshinEewViewer.csproj -p:PublishDir=../../tmp/$2_$3 -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=true -p:UseAppHost=true
rm "tmp/$2_$3/KyoshinEewViewer"
rm "tmp/$2_$3/*.dylib"
chmod +x tmp/$2_$3/KyoshinEewViewer
cd tmp/$2_$3; zip -r ../KyoshinEewViewer_ingen_$2_$3.zip *
