# Etapa 1: Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 80
EXPOSE 5001

# Etapa 2: Compilación y Restauración
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copia de definición de proyectos para cachear dependencias
COPY ["Directory.Packages.props", "./"]
COPY ["src/ApiProductos/ApiProductos.csproj", "src/ApiProductos/"]
RUN dotnet restore "src/ApiProductos/ApiProductos.csproj"

# Copia de todo el código fuente y compilación
COPY . .
WORKDIR "/src/src/ApiProductos"
RUN dotnet build "ApiProductos.csproj" -c Release -o /app/build

# Etapa 3: Publicación de Binarios
FROM build AS publish
RUN dotnet publish "ApiProductos.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 4: Imagen Final de Producción
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiProductos.dll"]
