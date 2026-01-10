# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["API.csproj", "."]
RUN dotnet restore "./API.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "API.csproj" -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish "API.csproj" -c Release -o /app/publish

# Final Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# استعمل المنفذ اللي Koyeb يبعثه (8080 افتراضي)
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
