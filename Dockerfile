# =========================
# Etapa de build (SDK 8.0)
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos los csproj primero para aprovechar caché en restore
COPY backend/global.sln ./
COPY backend/src/API/lexico/Lexico.API.csproj src/API/lexico/
COPY backend/src/Application/lexico/Lexico.Application.csproj src/Application/lexico/
COPY backend/src/Domain/lexico/Lexico.Domain.csproj src/Domain/lexico/
COPY backend/src/Infrastructure/lexico/Lexico.Infrastructure.csproj src/Infrastructure/lexico/

# Restauramos SOLO el proyecto API (resuelve también las referencias)
RUN dotnet restore src/API/lexico/Lexico.API.csproj

# Ahora copiamos todo el backend y publicamos
COPY backend/. .
# UseAppHost=false hace la salida portable (mejor para contenedores)
RUN dotnet publish src/API/lexico/Lexico.API.csproj -c Release -o /app/out /p:UseAppHost=false

# =========================
# Etapa de runtime (ASP.NET 8.0)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Railway inyecta PORT. ASPNETCORE_URLS hará que escuche en ese puerto.
ENV ASPNETCORE_URLS=http://+:${PORT}
# Si quieres un puerto por defecto local (no obligatorio):
ENV PORT=8080

# Copiamos la app publicada
COPY --from=build /app/out ./

# (Opcional) expone 8080 para correr local con `-p 8080:8080`
EXPOSE 8080

# Arranque
ENTRYPOINT ["dotnet", "Lexico.API.dll"]
