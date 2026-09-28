#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
#https://medium.com/@maheshwar.ramkrushna/best-practices-for-secure-docker-containerization-non-root-user-read-only-volumes-and-resource-d34ed09b1bd3

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Maroik.Core.Contract/Maroik.Core.Contract.csproj", "Maroik.Core.Contract/"]
COPY ["Maroik.Core.Domain/Maroik.Core.Domain.csproj", "Maroik.Core.Domain/"]
COPY ["Maroik.Core.Client/Maroik.Core.Client.csproj", "Maroik.Core.Client/"]
COPY ["Maroik.FileStorage/Maroik.FileStorage.csproj", "Maroik.FileStorage/"]

RUN dotnet restore "Maroik.FileStorage/Maroik.FileStorage.csproj" --disable-parallel
COPY . .
WORKDIR "/src/Maroik.FileStorage"
RUN dotnet build "Maroik.FileStorage.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Maroik.FileStorage.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Maroik.FileStorage.dll"]