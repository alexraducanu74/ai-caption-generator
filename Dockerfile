# This file is deprecated. Please use the service-specific Dockerfiles:
# - Dockerfile.api       - For .NET API backend
# - caption-service/Dockerfile - For Python AI service  
# - Dockerfile.web       - For Blazor WASM frontend
#
# These optimized Dockerfiles provide:
# - Better layer caching
# - Smaller image sizes
# - Proper health checks
# - Production-ready configurations
#
# For local development, use docker-compose:
#   docker-compose up -d
#
# For cloud deployment, see azure/Deploy-Azure.ps1

# This is a placeholder - use Dockerfile.api instead
FROM scratch
RUN echo "Use Dockerfile.api instead" && exit 1
