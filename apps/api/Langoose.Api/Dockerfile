FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["apps/api/Langoose.Api/Langoose.Api.csproj", "apps/api/Langoose.Api/"]
COPY ["apps/api/Langoose.Api/NuGet.Config", "apps/api/Langoose.Api/"]
COPY ["apps/api/Langoose.Data/Langoose.Data.csproj", "apps/api/Langoose.Data/"]
COPY ["apps/api/Langoose.Domain/Langoose.Domain.csproj", "apps/api/Langoose.Domain/"]
RUN dotnet restore "apps/api/Langoose.Api/Langoose.Api.csproj" --configfile apps/api/Langoose.Api/NuGet.Config

COPY . .
RUN dotnet publish "apps/api/Langoose.Api/Langoose.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Langoose.Api.dll"]
