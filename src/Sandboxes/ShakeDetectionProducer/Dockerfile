# Build stage with mise
FROM debian:12-slim AS build

RUN apt-get update \
    && apt-get -y --no-install-recommends install \
        curl git ca-certificates \
        libicu-dev \
    && rm -rf /var/lib/apt/lists/*

SHELL ["/bin/bash", "-o", "pipefail", "-c"]
ENV MISE_DATA_DIR="/mise"
ENV MISE_CONFIG_DIR="/mise"
ENV MISE_CACHE_DIR="/mise/cache"
ENV MISE_INSTALL_PATH="/usr/local/bin/mise"
ENV MISE_YES=1
ENV PATH="/mise/shims:$PATH"

RUN curl https://mise.run | sh

WORKDIR /src

# Copy mise.toml and install dotnet
COPY mise.toml mise.lock ./
RUN mise trust && mise install dotnet

# Copy project files for restore
COPY common.props ./
COPY nuget.config ./
COPY src/KyoshinEewViewer.Core/*.csproj ./src/KyoshinEewViewer.Core/
COPY src/KyoshinEewViewer.CustomControl/*.csproj ./src/KyoshinEewViewer.CustomControl/
COPY src/KyoshinEewViewer.DCReportParser/*.csproj ./src/KyoshinEewViewer.DCReportParser/
COPY src/KyoshinEewViewer.JmaXmlParser/*.csproj ./src/KyoshinEewViewer.JmaXmlParser/
COPY src/KyoshinEewViewer.Map/*.csproj ./src/KyoshinEewViewer.Map/
COPY src/KyoshinEewViewer.CsvSourceGenerator/*.csproj ./src/KyoshinEewViewer.CsvSourceGenerator/
COPY src/KyoshinEewViewer/*.csproj ./src/KyoshinEewViewer/
COPY src/Sandboxes/ShakeDetectionProducer/*.csproj ./src/Sandboxes/ShakeDetectionProducer/
COPY jma-code-dictionary/JmaCodeDictionary/*.csproj ./jma-code-dictionary/JmaCodeDictionary/

# Restore dependencies
RUN mise exec -- dotnet restore src/Sandboxes/ShakeDetectionProducer/ShakeDetectionProducer.csproj

# Copy all source
COPY . .

# Build self-contained binary
ARG TARGETARCH
RUN RUNTIME_ID=$([ "$TARGETARCH" = "amd64" ] && echo "linux-x64" || echo "linux-arm64") && \
    mise exec -- dotnet publish src/Sandboxes/ShakeDetectionProducer/ShakeDetectionProducer.csproj \
        -c Release \
        -r $RUNTIME_ID \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o /app/publish

# Runtime stage - self-contained binary only needs minimal dependencies
FROM debian:12-slim AS runtime
WORKDIR /app

# Install Skia dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libfreetype6 \
    libx11-6 \
    libxext6 \
    libxrender1 \
    libicu72 \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Make the binary executable
RUN chmod +x ShakeDetectionProducer

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV PORT=5000
ENV OTEL_SERVICE_NAME=shake-detection-producer
ENV KAFKA_BOOTSTRAP_SERVERS=localhost:9092
ENV KAFKA_TOPIC=shake-detect-events

EXPOSE 5000

ENTRYPOINT ["./ShakeDetectionProducer"]
