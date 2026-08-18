# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["Disney.Api/Disney.Api.csproj", "Disney.Api/"]
COPY ["Disney.Domain/Disney.Domain.csproj", "Disney.Domain/"]
COPY ["Disney.Application/Disney.Application.csproj", "Disney.Application/"]
COPY ["Disney.Infrastructure/Disney.Infrastructure.csproj", "Disney.Infrastructure/"]
RUN --mount=type=secret,id=nuget_credentials,env=NuGetPackageSourceCredentials_NordicTech \
    dotnet restore "./Disney.Api/Disney.Api.csproj" \
    --configfile "./NuGet.Config"
COPY . .
WORKDIR "/src/Disney.Api"
RUN dotnet publish "./Disney.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Disney.Api.dll"]
