# Збірка з кореня сервісу:  docker build -f Catalog.Api/Dockerfile -t catalog-api Services/Catalog
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Catalog.Domain/Catalog.Domain.csproj Catalog.Domain/
COPY Catalog.Dal/Catalog.Dal.csproj Catalog.Dal/
COPY Catalog.Bll/Catalog.Bll.csproj Catalog.Bll/
COPY Catalog.Api/Catalog.Api.csproj Catalog.Api/
RUN dotnet restore Catalog.Api/Catalog.Api.csproj

COPY . .
RUN dotnet publish Catalog.Api/Catalog.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
