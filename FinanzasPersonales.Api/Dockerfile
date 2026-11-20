FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FinanzasPersonales.Api/FinanzasPersonales.Api.csproj FinanzasPersonales.Api/
RUN dotnet restore FinanzasPersonales.Api/FinanzasPersonales.Api.csproj
COPY FinanzasPersonales.Api/ FinanzasPersonales.Api/
WORKDIR /src/FinanzasPersonales.Api
RUN dotnet build -c Release -o /app/build --no-restore
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "FinanzasPersonales.Api.dll"]
