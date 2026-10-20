FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY ./src .
WORKDIR /src/Endatix.WebHost
RUN dotnet restore -a "$TARGETARCH"
RUN dotnet publish -a "$TARGETARCH" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y curl
WORKDIR /app
COPY --from=build /app/publish .
# Third-party licences (Quartz.NET is Apache-2.0) ship with the image.
COPY THIRD-PARTY-NOTICES /app/THIRD-PARTY-NOTICES
# Opt-in file logging writes to logs/ under the content root, so the app user must own it.
RUN mkdir logs && chown "$APP_UID" logs
# Run as the image's non-root "app" user (uid 1654).
USER $APP_UID
ENTRYPOINT ["dotnet", "Endatix.WebHost.dll"]