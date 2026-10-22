FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Server/GameApi/ Server/GameApi/
COPY Server/MatchSettlement/ Server/MatchSettlement/
COPY Assets/Scripts/Prack/BackSys/GameData/UserMainData.cs Assets/Scripts/Prack/BackSys/GameData/
COPY Assets/Scripts/Prack/BackSys/GameData/MatchHistoryData.cs Assets/Scripts/Prack/BackSys/GameData/
COPY Assets/Scripts/Prack/BackSys/GameData/UserMatchHistoryData.cs Assets/Scripts/Prack/BackSys/GameData/
COPY Docs/BackendSeed/ Docs/BackendSeed/
RUN dotnet publish Server/GameApi/GameApi.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production PORT=8080
USER app
ENTRYPOINT ["dotnet", "GameApi.dll"]
