FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

COPY ["apps/api/NuGet.Config", "apps/api/"]
COPY ["apps/api/src/Langoose.Corpus.DbTool/Langoose.Corpus.DbTool.csproj", "apps/api/src/Langoose.Corpus.DbTool/"]
COPY ["apps/api/src/Langoose.Corpus.Data/Langoose.Corpus.Data.csproj", "apps/api/src/Langoose.Corpus.Data/"]

RUN dotnet restore "apps/api/src/Langoose.Corpus.DbTool/Langoose.Corpus.DbTool.csproj" --configfile apps/api/NuGet.Config

COPY . .

RUN dotnet build "apps/api/src/Langoose.Corpus.DbTool/Langoose.Corpus.DbTool.csproj" \
    --configuration Release \
    --no-restore \
    /p:RestoreConfigFile=/src/apps/api/NuGet.Config
