# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy common props
COPY common.props ./

# Copy project files
COPY src/KyoshinEewViewer.Core/*.csproj ./src/KyoshinEewViewer.Core/
COPY src/KyoshinEewViewer.CustomControl/*.csproj ./src/KyoshinEewViewer.CustomControl/
COPY src/KyoshinEewViewer.DCReportParser/*.csproj ./src/KyoshinEewViewer.DCReportParser/
COPY src/KyoshinEewViewer.JmaXmlParser/*.csproj ./src/KyoshinEewViewer.JmaXmlParser/
COPY src/KyoshinEewViewer.Map/*.csproj ./src/KyoshinEewViewer.Map/
COPY src/KyoshinEewViewer.CsvSourceGenerator/*.csproj ./src/KyoshinEewViewer.CsvSourceGenerator/
COPY src/KyoshinEewViewer/*.csproj ./src/KyoshinEewViewer/
COPY src/Sandboxes/ShakeDetectWebSocketServer/*.csproj ./src/Sandboxes/ShakeDetectWebSocketServer/
COPY jma-code-dictionary/JmaCodeDictionary/*.csproj ./jma-code-dictionary/JmaCodeDictionary/
COPY nuget.config ./

# Copy all source
WORKDIR /src
COPY . .

# Build
WORKDIR /src/src/Sandboxes/ShakeDetectWebSocketServer
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Install Skia dependencies
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libx11-6 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV PORT=5000
ENV OTEL_SERVICE_NAME=shake-detect-websocket-server

EXPOSE 5000

ENTRYPOINT ["dotnet", "ShakeDetectWebSocketServer.dll"]
