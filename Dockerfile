FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY BlackjackApp/BlackjackApp.csproj BlackjackApp/
RUN dotnet restore BlackjackApp/BlackjackApp.csproj

COPY . .
WORKDIR /src/BlackjackApp
RUN dotnet publish BlackjackApp.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BlackjackApp.dll"]
