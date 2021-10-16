#!/bin/bash

~/.dotnet/dotnet publish src/KyoshinEewViewer.Updater/KyoshinEewViewer.Updater.csproj -r $2 -c release -o tmp/$2_$3 -p:PublishReadyToRun=false -p:PublishSingleFile=true -p:PublishTrimmed=$4 --self-contained $4
chmod +x tmp/$2_$3/KyoshinEewViewer.Updater
cd tmp/$2_$3; zip -r ../KyoshinEewViewer_ingen_updater_$2_$3.zip *
