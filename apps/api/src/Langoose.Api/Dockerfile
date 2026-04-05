FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["apps/api/NuGet.Config", "apps/api/"]
COPY ["apps/api/src/Langoose.Api/Langoose.Api.csproj", "apps/api/src/Langoose.Api/"]
COPY ["apps/api/src/Langoose.Auth.Data/Langoose.Auth.Data.csproj", "apps/api/src/Langoose.Auth.Data/"]
COPY ["apps/api/src/Langoose.Data/Langoose.Data.csproj", "apps/api/src/Langoose.Data/"]
COPY ["apps/api/src/Langoose.Domain/Langoose.Domain.csproj", "apps/api/src/Langoose.Domain/"]
RUN dotnet restore "apps/api/src/Langoose.Api/Langoose.Api.csproj" --configfile apps/api/NuGet.Config

COPY . .
RUN dotnet publish "apps/api/src/Langoose.Api/Langoose.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Langoose.Api.dll"]
