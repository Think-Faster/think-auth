# Контекст сборки — корень репозитория (нужен vault-entrypoint.sh из корня).

# =========================
# Build
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Копируем всё решение
COPY AuthService/ .

# Восстанавливаем зависимости всего solution
RUN dotnet restore AuthService.sln

# Публикуем WebAPI
RUN dotnet publish WebAPI/WebAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================
# Runtime
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# bash, base64 — есть в образе; curl, jq — для vault-entrypoint.sh (curl — ещё и для healthcheck)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl jq \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

COPY vault-entrypoint.sh /usr/local/bin/vault-entrypoint.sh
RUN chmod +x /usr/local/bin/vault-entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# Секреты забирает из Vault при каждом старте контейнера, затем exec приложения.
ENTRYPOINT ["/usr/local/bin/vault-entrypoint.sh"]
CMD ["dotnet", "WebAPI.dll"]
