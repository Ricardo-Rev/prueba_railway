# =========================
# Etapa de build (SDK 8.0)
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos csproj primero para cachear el restore
COPY global.sln ./
COPY src/API/lexico/Lexico.API.csproj src/API/lexico/
COPY src/Application/lexico/Lexico.Application.csproj src/Application/lexico/
COPY src/Domain/lexico/Lexico.Domain.csproj src/Domain/lexico/
COPY src/Infrastructure/lexico/Lexico.Infrastructure.csproj src/Infrastructure/lexico/
RUN dotnet restore src/API/lexico/Lexico.API.csproj

# Ahora copiamos TODO el backend y publicamos
COPY . .
RUN dotnet publish src/API/lexico/Lexico.API.csproj -c Release -o /app/out /p:UseAppHost=false

# =========================
# Etapa de runtime (ASP.NET 8.0)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Railway inyecta PORT; ASPNETCORE_URLS lo usa para escuchar
ENV ASPNETCORE_URLS=http://+:${PORT}
# Puerto por defecto local (opcional)
ENV PORT=8080

# Copiamos la app publicada
COPY --from=build /app/out ./

# Exponer 8080 para correr local (opcional)
EXPOSE 8080

ENTRYPOINT ["dotnet", "Lexico.API.dll"]
