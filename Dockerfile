FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Victoria.API/Victoria.API.csproj", "src/Victoria.API/"]
COPY ["src/Victoria.Core/Victoria.Core.csproj", "src/Victoria.Core/"]
COPY ["src/Victoria.Infrastructure/Victoria.Infrastructure.csproj", "src/Victoria.Infrastructure/"]
COPY ["src/Victoria.Inventory/Victoria.Inventory.csproj", "src/Victoria.Inventory/"]
RUN dotnet restore "src/Victoria.API/Victoria.API.csproj"
COPY . .
WORKDIR "/src/src/Victoria.API"
RUN dotnet build "Victoria.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Victoria.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Victoria.API.dll"]
