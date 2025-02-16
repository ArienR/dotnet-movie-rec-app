# Stage 1: Base image with ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5003

# Ensure Blazor app listens on all interfaces
ENV ASPNETCORE_URLS=http://+:5003

# Stage 2: Build application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MovieRecApp.Client/MovieRecApp.Client.csproj", "MovieRecApp.Client/"]
COPY ["MovieRecApp.Shared/MovieRecApp.Shared.csproj", "MovieRecApp.Shared/"]
RUN dotnet restore "MovieRecApp.Client/MovieRecApp.Client.csproj"
COPY . .
WORKDIR "/src/MovieRecApp.Client"
RUN dotnet build "MovieRecApp.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MovieRecApp.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final runtime container
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MovieRecApp.Client.dll"]
