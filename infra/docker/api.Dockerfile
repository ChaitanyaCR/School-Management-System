# syntax=docker/dockerfile:1
# API image. Build context: repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY .editorconfig backend/global.json backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/ ./src/
RUN dotnet restore src/SchoolMs.Api/SchoolMs.Api.csproj

# The OpenAPI document is committed; skip regenerating it inside the image.
RUN dotnet publish src/SchoolMs.Api/SchoolMs.Api.csproj \
      --configuration Release --no-restore --output /app \
      -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

# Chiseled: minimal Ubuntu image with no shell or package manager; runs as non-root by default.
# The "-extra" variant adds ICU and tzdata, needed for IST time zone handling and Indian culture formatting.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolMs.Api.dll"]
