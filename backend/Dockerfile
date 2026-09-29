# API do BarcaLog — imagem de produção.
#   docker build -t barcalog-api -f backend/Dockerfile backend
#   docker run -p 8080:8080 -e ConnectionStrings__BarcaLog=... -e Jwt__Chave=... -e Seguranca__ChaveCriptografia=... barcalog-api
#
# - Build separado do runtime (a imagem final não tem SDK nem código-fonte).
# - Roda como usuário sem privilégio ("app", UID 1654, já existente na imagem oficial).
# - Nenhum segredo na imagem: tudo por variável de ambiente / secret do orquestrador.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props BarcaLog.sln ./
COPY src/BarcaLog.Domain/BarcaLog.Domain.csproj src/BarcaLog.Domain/
COPY src/BarcaLog.Application/BarcaLog.Application.csproj src/BarcaLog.Application/
COPY src/BarcaLog.Infrastructure/BarcaLog.Infrastructure.csproj src/BarcaLog.Infrastructure/
COPY src/BarcaLog.Api/BarcaLog.Api.csproj src/BarcaLog.Api/
RUN dotnet restore src/BarcaLog.Api/BarcaLog.Api.csproj
COPY src/ src/
RUN dotnet publish src/BarcaLog.Api/BarcaLog.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 8080
USER $APP_UID
# Sondas: configure no orquestrador (a imagem aspnet não tem curl, de propósito):
#   liveness  → GET /health/live     readiness → GET /health/ready (checa o banco)
ENTRYPOINT ["dotnet", "BarcaLog.Api.dll"]
