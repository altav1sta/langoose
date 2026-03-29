FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Langoose.Api.csproj", "./"]
COPY ["NuGet.Config", "./"]
RUN dotnet restore "Langoose.Api.csproj" --configfile NuGet.Config

COPY . .
RUN dotnet publish "Langoose.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Langoose.Api.dll"]
