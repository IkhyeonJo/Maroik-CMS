#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["MyLaboratory.WebSite/MyLaboratory.WebSite.csproj", "MyLaboratory.WebSite/"]
COPY ["MyLaboratory.Common.DataAccess/MyLaboratory.Common.DataAccess.csproj", "MyLaboratory.Common.DataAccess/"]
RUN dotnet restore "MyLaboratory.WebSite/MyLaboratory.WebSite.csproj"
COPY . .
WORKDIR "/src/MyLaboratory.WebSite"
RUN dotnet build "MyLaboratory.WebSite.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyLaboratory.WebSite.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyLaboratory.WebSite.dll"]
