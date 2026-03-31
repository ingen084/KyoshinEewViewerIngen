FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY common.props ./
COPY nuget.config ./
COPY src/KyoshinEewViewer.Core/*.csproj ./src/KyoshinEewViewer.Core/
COPY src/Sandboxes/ReplayGenerator/*.csproj ./src/Sandboxes/ReplayGenerator/

RUN dotnet restore src/Sandboxes/ReplayGenerator/ReplayGenerator.csproj

COPY . .

ARG TARGETARCH
RUN dotnet publish src/Sandboxes/ReplayGenerator/ReplayGenerator.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN chmod +x ReplayGenerator

ENV PORT=5000
ENV TZ=Asia/Tokyo

EXPOSE 5000

ENTRYPOINT ["./ReplayGenerator"]
