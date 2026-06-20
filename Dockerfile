FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DARAK.Api/DARAK.Api.csproj", "DARAK.Api/"]
RUN dotnet restore "DARAK.Api/DARAK.Api.csproj"
COPY . .
WORKDIR "/src/DARAK.Api"
RUN dotnet publish "DARAK.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/logs /app/App_Data/Uploads/Documents \
    && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "DARAK.Api.dll"]
