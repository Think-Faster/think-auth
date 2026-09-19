# =========================
# Build
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY WebAPI/WebAPI.csproj WebAPI/

RUN dotnet restore WebAPI/WebAPI.csproj

COPY WebAPI/ WebAPI/

WORKDIR /src/WebAPI

RUN dotnet publish WebAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================
# Runtime
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "WebAPI.dll"]