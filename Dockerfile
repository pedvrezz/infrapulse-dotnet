FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/InfraPulse/InfraPulse.csproj src/InfraPulse/
RUN dotnet restore src/InfraPulse/InfraPulse.csproj
COPY . .
RUN dotnet publish src/InfraPulse/InfraPulse.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "infrapulse.dll"]
