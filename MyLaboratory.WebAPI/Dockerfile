#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["MyLaboratory.WebAPI/MyLaboratory.WebAPI.csproj", "MyLaboratory.WebAPI/"]
COPY ["MyLaboratory.Common.DataAccess/MyLaboratory.Common.DataAccess.csproj", "MyLaboratory.Common.DataAccess/"]
RUN dotnet restore "MyLaboratory.WebAPI/MyLaboratory.WebAPI.csproj"
COPY . .
WORKDIR "/src/MyLaboratory.WebAPI"
RUN dotnet build "MyLaboratory.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyLaboratory.WebAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyLaboratory.WebAPI.dll"]