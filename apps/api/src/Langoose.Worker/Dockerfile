FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["apps/api/NuGet.Config", "apps/api/"]
COPY ["apps/api/src/Langoose.Worker/Langoose.Worker.csproj", "apps/api/src/Langoose.Worker/"]
COPY ["apps/api/src/Langoose.Core/Langoose.Core.csproj", "apps/api/src/Langoose.Core/"]
COPY ["apps/api/src/Langoose.Data/Langoose.Data.csproj", "apps/api/src/Langoose.Data/"]
COPY ["apps/api/src/Langoose.Domain/Langoose.Domain.csproj", "apps/api/src/Langoose.Domain/"]
RUN dotnet restore "apps/api/src/Langoose.Worker/Langoose.Worker.csproj" --configfile apps/api/NuGet.Config

COPY . .
RUN dotnet publish "apps/api/src/Langoose.Worker/Langoose.Worker.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Langoose.Worker.dll"]
