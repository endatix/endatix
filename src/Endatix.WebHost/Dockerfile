FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY ./src .
WORKDIR /src/Endatix.WebHost
RUN dotnet restore -a $TARGETARCH
RUN dotnet publish -a $TARGETARCH -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y curl
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Endatix.WebHost.dll"]