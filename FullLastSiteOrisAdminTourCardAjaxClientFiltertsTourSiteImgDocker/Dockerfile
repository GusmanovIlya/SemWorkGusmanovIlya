# ===== build stage =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Server/Server.csproj Server/
RUN dotnet restore Server/Server.csproj

COPY . .
RUN dotnet publish Server/Server.csproj -c Release -o /app/publish

# ===== runtime stage =====
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

COPY --from=build /app/publish .
EXPOSE 1234

ENTRYPOINT ["dotnet", "Server.dll"]
