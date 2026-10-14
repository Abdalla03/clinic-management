# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ClinicManagement.Domain/ClinicManagement.Domain.csproj src/ClinicManagement.Domain/
COPY src/ClinicManagement.Application/ClinicManagement.Application.csproj src/ClinicManagement.Application/
COPY src/ClinicManagement.Infrastructure/ClinicManagement.Infrastructure.csproj src/ClinicManagement.Infrastructure/
COPY src/ClinicManagement.Api/ClinicManagement.Api.csproj src/ClinicManagement.Api/
RUN dotnet restore src/ClinicManagement.Api/ClinicManagement.Api.csproj

COPY . .
RUN dotnet publish src/ClinicManagement.Api/ClinicManagement.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "ClinicManagement.Api.dll"]
