# syntax=docker/dockerfile:1

# Used when running from Visual Studio in Fast mode
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app


# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# NuGet sources - no credentials in this file
COPY ["NuGet.Config", "./"]

# Copy project files first to take advantage of Docker layer caching
COPY ["Worker/Worker.csproj", "Worker/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]

# Credentials are available only during this RUN
RUN --mount=type=secret,id=nuget_credentials,env=NuGetPackageSourceCredentials_NordicTech \
    dotnet restore "./Worker/Worker.csproj" \
    --configfile "./NuGet.Config"

# Copy remaining source code
COPY . .

WORKDIR "/src/Worker"

RUN dotnet build "./Worker.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore


# Publish
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./Worker.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore


# Final
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Worker.dll"]