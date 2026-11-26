FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Server/GameApi/ Server/GameApi/
COPY Server/MatchSettlement/ Server/MatchSettlement/
COPY Assets/Scripts/Prack/BackSys/GameData/ Assets/Scripts/Prack/BackSys/GameData/
COPY Assets/Resources/Gameplay/ShopCurrencyPacks20261126/ Assets/Resources/Gameplay/ShopCurrencyPacks20261126/
COPY Assets/Resources/Gameplay/DailyCues20261126/ Assets/Resources/Gameplay/DailyCues20261126/
COPY Assets/Resources/Gameplay/PremiumCues/ Assets/Resources/Gameplay/PremiumCues/
COPY Assets/Resources/Gameplay/level-up-*.png Assets/Resources/Gameplay/
COPY Docs/BackendSeed/ Docs/BackendSeed/
RUN dotnet publish Server/GameApi/GameApi.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data/shop-images && chown -R app:app /app/App_Data
ENV ASPNETCORE_ENVIRONMENT=Production PORT=8080
USER app
ENTRYPOINT ["dotnet", "GameApi.dll"]
