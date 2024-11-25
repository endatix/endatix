FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ./src .
WORKDIR /src/Endatix.WebHost
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update && apt-get install -y curl
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Endatix.WebHost.dll"]
