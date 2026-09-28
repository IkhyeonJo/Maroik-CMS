#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
#https://medium.com/@maheshwar.ramkrushna/best-practices-for-secure-docker-containerization-non-root-user-read-only-volumes-and-resource-d34ed09b1bd3

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

EXPOSE 80
EXPOSE 443

# Compile the custom client scripts (TypeScripts/**/*.ts -> wwwroot/**/*.js). The .NET
# SDK image has no Node, so this dedicated stage generates them (they are git-ignored,
# not committed). See Maroik.Website/TypeScripts/README.md.
#
# TS_SOURCEMAPS: default "false" -> `npm run build` (tsconfig.build.json): no site.js.map,
# no sourceMappingURL comment -> nothing to strip from a production image.
# docker-compose.debug.yml sets it "true" -> `npm run build:debug` (tsconfig.json) also
# emits wwwroot/**/custom/**/site.js.map (with .ts inlined), so container debugging in
# Rider/VS maps site.js back to site.ts. Leave it "false" for a release image.
#
# The jsdom test suite is OFF by default so Rider/VS "regular mode" docker-compose
# debug builds aren't gated on a ~40s test run. Opt in with:
#     docker build --build-arg RUN_CLIENT_TESTS=true -f Maroik.Website/Dockerfile .
FROM node:22-alpine AS tsbuild
ARG RUN_CLIENT_TESTS=false
ARG TS_SOURCEMAPS=false
WORKDIR /ts
COPY ["Maroik.Website/package.json", "Maroik.Website/package-lock.json", "Maroik.Website/.npmrc", "./"]
RUN npm ci --no-audit --no-fund
COPY ["Maroik.Website/tsconfig.json", "Maroik.Website/tsconfig.build.json", "Maroik.Website/vitest.config.mts", "./"]
COPY ["Maroik.Website/TypeScripts", "./TypeScripts"]
COPY ["Maroik.Website/TypeScripts.Tests", "./TypeScripts.Tests"]
RUN if [ "$TS_SOURCEMAPS" = "true" ]; then npm run build:debug; else npm run build; fi && \
    if [ "$RUN_CLIENT_TESTS" = "true" ]; then npx vitest run; \
    else echo "skipping client-script tests (build with --build-arg RUN_CLIENT_TESTS=true to run them)"; fi

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Maroik.Website/Maroik.Website.csproj", "Maroik.Website/"]
COPY ["Maroik.Core.Contract/Maroik.Core.Contract.csproj", "Maroik.Core.Contract/"]
COPY ["Maroik.Core.Domain/Maroik.Core.Domain.csproj", "Maroik.Core.Domain/"]
COPY ["Maroik.Core.PostgreSQL/Maroik.Core.PostgreSQL.csproj", "Maroik.Core.PostgreSQL/"]
COPY ["Maroik.Core.Repository/Maroik.Core.Repository.csproj", "Maroik.Core.Repository/"]
COPY ["Maroik.Core.Client/Maroik.Core.Client.csproj", "Maroik.Core.Client/"]
COPY ["Maroik.Core.Service/Maroik.Core.Service.csproj", "Maroik.Core.Service/"]
RUN dotnet restore "Maroik.Website/Maroik.Website.csproj" --disable-parallel
COPY . .
# Overlay the freshly-compiled client scripts from the Node stage (no node_modules
# here, so the csproj CompileClientTypeScript target is a no-op and won't run tsc).
COPY --from=tsbuild /ts/wwwroot/ Maroik.Website/wwwroot/
WORKDIR "/src/Maroik.Website"
RUN dotnet build "Maroik.Website.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Maroik.Website.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Maroik.Website.dll"]
