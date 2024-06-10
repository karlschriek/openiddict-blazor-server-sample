FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OpenIddict.Blazor.Server/OpenIddict.Blazor.Server.csproj", "OpenIddict.Blazor.Server/"]
RUN dotnet restore "OpenIddict.Blazor.Server/OpenIddict.Blazor.Server.csproj"
COPY . .
WORKDIR "/src/OpenIddict.Blazor.Server"
RUN dotnet build "OpenIddict.Blazor.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "OpenIddict.Blazor.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenIddict.Blazor.Server.dll"]
