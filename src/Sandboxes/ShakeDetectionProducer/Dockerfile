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
RUN dotnet publish src/Sandboxes/ShakeDetectionProducer/ShakeDetectionProducer.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o /app/publish

# Download observation points data
ARG OBSERVATION_POINTS_TAG=v2025.12.27
RUN curl -fsSL -o /app/publish/observation-points.kmop \
    https://github.com/ingen084/kyoshin-monitor-observation-points/releases/download/${OBSERVATION_POINTS_TAG}/intensity-points-v2.kmop

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
ENV OBSERVATION_POINTS_PATH=/app/observation-points.kmop
ENV TZ=Asia/Tokyo

EXPOSE 5000

ENTRYPOINT ["./ShakeDetectionProducer"]
