# Etapa 1: Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 80
EXPOSE 8082

# Etapa 2: Compilación y Restauración
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copia de definición de proyectos para cachear dependencias
COPY ["Directory.Packages.props", "./"]
COPY ["src/ApiVehiculos/ApiVehiculos.csproj", "src/ApiVehiculos/"]
RUN dotnet restore "src/ApiVehiculos/ApiVehiculos.csproj"

# Copia de todo el código fuente y compilación
COPY . .
WORKDIR "/src/src/ApiVehiculos"
RUN dotnet build "ApiVehiculos.csproj" -c Release -o /app/build

# Etapa 3: Publicación de Binarios
FROM build AS publish
RUN dotnet publish "ApiVehiculos.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 4: Imagen Final de Producción
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiVehiculos.dll"]
