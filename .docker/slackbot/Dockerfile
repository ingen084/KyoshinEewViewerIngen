FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy everything
COPY . ./

WORKDIR /app/src/Sandboxes/SlackBot
RUN dotnet publish -c Release -r linux-x64 -o /app/out --no-self-contained

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
RUN apt-get update; apt-get install libfontconfig1 libfreetype6 -y
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SlackBot.dll"]
