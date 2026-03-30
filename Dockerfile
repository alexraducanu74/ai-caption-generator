FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["CaptionGenerator.API/CaptionGenerator.API.csproj", "CaptionGenerator.API/"]
COPY ["CaptionGenerator.Application/CaptionGenerator.Application.csproj", "CaptionGenerator.Application/"]
COPY ["CaptionGenerator.Domain/CaptionGenerator.Domain.csproj", "CaptionGenerator.Domain/"]
COPY ["CaptionGenerator.Infrastructure/CaptionGenerator.Infrastructure.csproj", "CaptionGenerator.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "CaptionGenerator.API/CaptionGenerator.API.csproj"

# Copy source code (includes migrations)
COPY . .

# Publish
FROM build AS publish
WORKDIR "/src/CaptionGenerator.API"
RUN dotnet publish "CaptionGenerator.API.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Ensure upload directory exists inside the container
RUN mkdir -p /app/wwwroot/uploads/images

EXPOSE 5000 5001

ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "CaptionGenerator.API.dll"]
