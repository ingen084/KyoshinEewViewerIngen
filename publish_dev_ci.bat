del /Q out\*
rmdir out
dotnet tool restore
dotnet publish src\KyoshinEewViewer\KyoshinEewViewer.csproj -c Debug -o out
pause