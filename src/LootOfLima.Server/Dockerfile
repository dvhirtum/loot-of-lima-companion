# The server, built from the repository root.
#
# Build context is the root rather than this folder, because the project references four sibling
# projects and reads Directory.Build.props, Directory.Packages.props and global.json from above it.
# A context of src/LootOfLima.Server would see none of them.

# The floating 10.0 tag rather than a pinned patch, because global.json is copied in below and is
# the thing that decides which SDK is acceptable. If the image ever moved to an SDK the pin will
# not accept, the build fails here and says so, which is exactly what the pin is for -- a container
# quietly building against the wrong SDK is the failure it exists to prevent.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before the source is copied, so a source edit does not re-download the world. The csproj
# files and the three build files are all restore reads, and they change far less often than code.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/LootOfLima.Rules/LootOfLima.Rules.csproj src/LootOfLima.Rules/
COPY src/LootOfLima.Contracts/LootOfLima.Contracts.csproj src/LootOfLima.Contracts/
COPY src/LootOfLima.Projection/LootOfLima.Projection.csproj src/LootOfLima.Projection/
COPY src/LootOfLima.Scenarios/LootOfLima.Scenarios.csproj src/LootOfLima.Scenarios/
COPY src/LootOfLima.Server/LootOfLima.Server.csproj src/LootOfLima.Server/
RUN dotnet restore src/LootOfLima.Server/LootOfLima.Server.csproj

COPY src/ src/
RUN dotnet publish src/LootOfLima.Server/LootOfLima.Server.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Named so the port is stated rather than inherited. The aspnet images already default to 8080 and
# run as a non-root user, which is why this is 8080 and not 80.
#
# The environment is deliberately not set here: the image should not have an opinion about where it
# is running. compose.yaml chooses it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./
USER $APP_UID
ENTRYPOINT ["dotnet", "LootOfLima.Server.dll"]
