FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.3

ENV PATH="/tools:${PATH}"

COPY ["apps/api/NuGet.Config", "apps/api/"]
COPY ["apps/api/src/Langoose.DbTool/Langoose.DbTool.csproj", "apps/api/src/Langoose.DbTool/"]
COPY ["apps/api/src/Langoose.Data/Langoose.Data.csproj", "apps/api/src/Langoose.Data/"]
COPY ["apps/api/src/Langoose.Auth.Data/Langoose.Auth.Data.csproj", "apps/api/src/Langoose.Auth.Data/"]
COPY ["apps/api/src/Langoose.Domain/Langoose.Domain.csproj", "apps/api/src/Langoose.Domain/"]

RUN dotnet restore "apps/api/src/Langoose.DbTool/Langoose.DbTool.csproj" --configfile apps/api/NuGet.Config

COPY . .

RUN dotnet build "apps/api/src/Langoose.DbTool/Langoose.DbTool.csproj" \
    --configuration Release \
    --no-restore \
    /p:RestoreConfigFile=/src/apps/api/NuGet.Config
