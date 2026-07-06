# =============================================================
# ESTÁGIO 1: BASE (Runtime)
# Define a imagem leve que vai rodar em produção.
# Declarada primeiro para ser referenciada pelo estágio final.
# =============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# =============================================================
# ESTÁGIO 2: BUILD
# Imagem completa do SDK .NET 9 (~800MB).
# Usada apenas para restaurar dependências e compilar.
# =============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /


# Copiamos os .csproj ANTES do código-fonte.
# Motivo: o Docker tem cache por camadas. Se os .csproj não mudarem,
# o 'dotnet restore' abaixo é pulado no próximo build — muito mais rápido.
# Se copiássemos tudo de uma vez, qualquer mudança de código invalidaria
# o cache do restore, baixando todos os pacotes NuGet novamente.
COPY ["src/JADirect.FleetOps/JADirect.Web/JADirect.Web.csproj",             "src/JADirect.FleetOps/JADirect.Web/"]
COPY ["src/JADirect.FleetOps/JADirect.Application/JADirect.Application.csproj", "src/JADirect.FleetOps/JADirect.Application/"]
COPY ["src/JADirect.FleetOps/JADirect.Data/JADirect.Data.csproj",           "src/JADirect.FleetOps/JADirect.Data/"]
COPY ["src/JADirect.FleetOps/JADirect.Domain/JADirect.Domain.csproj",       "src/JADirect.FleetOps/JADirect.Domain/"]

# Restaura os pacotes NuGet de todos os projetos.
RUN dotnet restore "src/JADirect.FleetOps/JADirect.Web/JADirect.Web.csproj"

# Copia o restante do código-fonte.
COPY . .

# =============================================================
# ESTÁGIO 3: PUBLISH
# Compila e publica em modo Release.
# /p:UseAppHost=false evita gerar um executável nativo (.exe),
# desnecessário em containers Linux.
# =============================================================
FROM build AS publish
WORKDIR "/src/JADirect.FleetOps/JADirect.Web"
RUN dotnet publish "JADirect.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# =============================================================
# ESTÁGIO 4: FINAL
# Parte da imagem base leve (estágio 1) e copia apenas
# os arquivos publicados do estágio anterior.
# Este é o container que vai rodar no Railway.
# =============================================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JADirect.Web.dll"]