#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
#https://medium.com/@maheshwar.ramkrushna/best-practices-for-secure-docker-containerization-non-root-user-read-only-volumes-and-resource-d34ed09b1bd3

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Maroik.WebSite/Maroik.WebSite.csproj", "Maroik.WebSite/"]
COPY ["Maroik.Common.DataAccess/Maroik.Common.DataAccess.csproj", "Maroik.Common.DataAccess/"]
COPY ["Maroik.Common.Miscellaneous/Maroik.Common.Miscellaneous.csproj", "Maroik.Common.Miscellaneous/"]
RUN dotnet restore "Maroik.WebSite/Maroik.WebSite.csproj" --disable-parallel
COPY . .
WORKDIR "/src/Maroik.WebSite"
RUN dotnet build "Maroik.WebSite.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Maroik.WebSite.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Maroik.WebSite.dll"]
