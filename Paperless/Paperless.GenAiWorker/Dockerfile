# ---- Build (SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files needed for restore
COPY Paperless/Paperless.Contracts/Paperless.Contracts.csproj   Paperless/Paperless.Contracts/
COPY Paperless/Paperless.GenAiWorker/Paperless.GenAiWorker.csproj   Paperless/Paperless.GenAiWorker/

# Restore worker (brings in referenced projects via ProjectReference)
RUN dotnet restore Paperless/Paperless.GenAiWorker/Paperless.GenAiWorker.csproj

# Copy full source tree
COPY Paperless/ Paperless/

# Publish worker
WORKDIR /src/Paperless/Paperless.GenAiWorker
RUN dotnet publish -c Release -o /app

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app .

# Run the GenAI worker
ENTRYPOINT ["dotnet", "Paperless.GenAiWorker.dll"]
