# Build stage with official .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files for restore
COPY common.props ./
COPY nuget.config ./
COPY src/KyoshinEewViewer.Core/*.csproj ./src/KyoshinEewViewer.Core/
COPY src/Sandboxes/ShakeDetectionProducer/*.csproj ./src/Sandboxes/ShakeDetectionProducer/

# Restore dependencies
RUN dotnet restore src/Sandboxes/ShakeDetectionProducer/ShakeDetectionProducer.csproj

# Copy all source
COPY . .

# Build self-contained binary
ARG TARGETARCH
RUN RUNTIME_ID=$([ "$TARGETARCH" = "amd64" ] && echo "linux-x64" || echo "linux-arm64") && \
    dotnet publish src/Sandboxes/ShakeDetectionProducer/ShakeDetectionProducer.csproj \
        -c Release \
        -r $RUNTIME_ID \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o /app/publish

# Runtime stage - use runtime-deps for self-contained binaries
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

# Install Skia dependencies (not included in runtime-deps)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
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
ENV TIMER_OFFSET_MS=1100

# OBSERVATION_POINTS_PATH must be set at runtime
# Example: -v /path/to/observation-points.kmop:/data/points.kmop -e OBSERVATION_POINTS_PATH=/data/points.kmop

EXPOSE 5000

ENTRYPOINT ["./ShakeDetectionProducer"]
