# ---- Base runtime (ASP.NET Core) ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# ---- Build stage (SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files (solution-root relative) for layer-cached restore
COPY Paperless/Paperless.Rest/Paperless.Rest.csproj                 Paperless/Paperless.Rest/
COPY Paperless/Paperless.Application/Paperless.Application.csproj   Paperless/Paperless.Application/
COPY Paperless/Paperless.Infrastructure/Paperless.Infrastructure.csproj Paperless/Paperless.Infrastructure/
COPY Paperless/Paperless.Domain/Paperless.Domain.csproj             Paperless/Paperless.Domain/
COPY Paperless/Paperless.Shared/Paperless.Shared.csproj             Paperless/Paperless.Shared/
COPY Paperless/Paperless.Contracts/Paperless.Contracts.csproj   Paperless/Paperless.Contracts/

# Restore
RUN dotnet restore Paperless/Paperless.Rest/Paperless.Rest.csproj

# Copy full source tree (solution-root -> image)
COPY Paperless/ Paperless/

# Publish
WORKDIR /src/Paperless/Paperless.Rest
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---- Final image ----
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Paperless.Rest.dll"]
