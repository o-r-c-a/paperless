FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Paperless/Paperless.IndexWorker/Paperless.IndexWorker.csproj Paperless/Paperless.IndexWorker/
COPY Paperless/Paperless.Contracts/Paperless.Contracts.csproj Paperless/Paperless.Contracts/
COPY Paperless/Paperless.Shared/Paperless.Shared.csproj Paperless/Paperless.Shared/

RUN dotnet restore Paperless/Paperless.IndexWorker/Paperless.IndexWorker.csproj

COPY Paperless/ Paperless/

WORKDIR /src/Paperless/Paperless.IndexWorker
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Paperless.IndexWorker.dll"]