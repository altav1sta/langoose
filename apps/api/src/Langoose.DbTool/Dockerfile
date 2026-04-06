FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

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
