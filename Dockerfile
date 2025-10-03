# =========================
# Etapa de build (SDK 8.0)
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos proyectos para restaurar dependencias
COPY global.sln ./
COPY src/API/lexico/Lexico.API.csproj src/API/lexico/
COPY src/Application/lexico/Lexico.Application.csproj src/Application/lexico/
COPY src/Domain/lexico/Lexico.Domain.csproj src/Domain/lexico/
COPY src/Infrastructure/lexico/Lexico.Infrastructure.csproj src/Infrastructure/lexico/

RUN dotnet restore src/API/lexico/Lexico.API.csproj

# Copiamos el resto del código y publicamos
COPY . .
RUN dotnet publish src/API/lexico/Lexico.API.csproj -c Release -o /app/out

# =========================
# Etapa final (ASP.NET 8)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Para correr localmente con -p 8080:8080 (Railway inyecta $PORT)
EXPOSE 8080

# Copiamos artefactos publicados
COPY --from=build /app/out ./

# Importante: NO seteamos ASPNETCORE_URLS aquí.
ENTRYPOINT ["dotnet", "Lexico.API.dll"]
