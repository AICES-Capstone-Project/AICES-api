# ============================
# BASE IMAGE (.NET 9 runtime)
# ============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Set timezone to Vietnam
RUN apt-get update && apt-get install -y tzdata \
    && ln -fs /usr/share/zoneinfo/Asia/Ho_Chi_Minh /etc/localtime \
    && echo "Asia/Ho_Chi_Minh" > /etc/timezone \
    && dpkg-reconfigure -f noninteractive tzdata

WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# ============================
# BUILD IMAGE (.NET 9 SDK)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["API/API.csproj", "API/"]
COPY ["DataAccessLayer/DataAccessLayer.csproj", "DataAccessLayer/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["BusinessObjectLayer/BusinessObjectLayer.csproj", "BusinessObjectLayer/"]

# Restore dependencies
RUN dotnet restore "API/API.csproj"

# Copy source and publish
COPY . .
WORKDIR "/src/API"
RUN dotnet publish "API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============================
# FINAL IMAGE
# ============================
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
