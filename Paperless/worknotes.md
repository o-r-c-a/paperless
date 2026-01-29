UI:			http://localhost/
Swagger:	http://localhost:8080/swagger
RabbitMQ Management: http://localhost:15672/
PostgreSQL Adminer: http://localhost:9091/

## Packages installed in Paperless.Infrastructure:
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Design
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.EntityFrameworkCore.Tools
- Minio
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Options.ConfigurationExtensions

## Packages installed in Paperless.Application:
- AutoMapper
- MediatR

## Packages installed in Paperless.Domain:
- MediatR

## Packages installed in Paperless.Rest:
- Microsoft.EntityFrameworkCore.Design
- AutoMapper
- FluentValidation
- Swashbuckle.AspNetCore
- Serilog.Sinks.Console
- Serilog.Sinks.File
- Serilog.AspNetCore
- Serilog.Settings.Configuration
- FluentValidation.DependencyInjectionExtensions
- RabbitMQ.Client
- Minio
- MediatR

## Packages installed in Paperless.Shared:
- Serilog.Sinks.Console
- Serilog.Sinks.File
- Serilog.AspNetCore
- Serilog.Settings.Configuration

## Packages installed in Paperless.OcrWorker:
- RabbitMQ.Client
- Tesseract
- Ghostscript.NET
- Minio
- Serilog
- Serilog.Sinks.Console
- Serilog.Sinks.File
- Serilog.AspNetCore
- Serilog.Settings.Configuration

## Packages installed in Paperless.Tests:
- NSubstitute
- NSubstitute.Analyzers.CSharp


## Githooks
It is recommended to implement the hooks inside the .githooks folder.
You can either:
### 1
Drag the contents into your .git/hooks folder.
### 2
Execute:
`git config core.hooksPath .githooks`


## Docker commands:
In repo root; will recreate/restart containers as needed:
`docker compose up -d`
--> Attention: after this command, the application is already running inside the container. No need to start it in VS!

In repo root; will rebuild images; use this when we changed C# code or project refs:
`docker compose up -d --build`
check status:
`docker ps`
tail db logs:
`docker logs -f paperless_postgres`
tail server logs:
`docker logs -f paperless_rest`
stops & removes containers, but keeps volumes:
`docker compose down`

when we changed the Dockerfile, run:
`docker compose down`
`docker image rm sem_project-rest:latest`
``docker compose up -d --build``
or: ``docker compose up -d --build --remove-orphans``

Full reset (removes containers & images & volumes = wipes DB data):
``docker compose down --rmi all --volumes --remove-orphans``

Check whether uploaded file exists in the folder:
`docker compose exec rest sh -lc 'ls -l /app/uploads'`

## Package Manager Console commands:
Add a migration:
(Default project: Paperless.Infrastructure)
``Add-Migration AddSomething -StartupProject Paperless.Rest``

Apply migrations to the running Postgres container:
``Update-Database -StartupProject Paperless.Rest``

## Swagger: Samples to test endpoints:
http://localhost:8080/swagger

POST
{
  "name": "TestDoc.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 12345
}

## RabbitMQ quick checks:
### Queues, message counts, consumers (inside container)
``docker exec paperless_rabbitmq rabbitmqctl list_queues name messages consumers``

### Check health state quickly
``docker inspect -f '{{.State.Health.Status}}' paperless_rabbitmq``

### Open the UI (paperless / paperless)
``open http://localhost:15672``

## Adminer quick checks:
Open
``http://localhost:9091``
### Login:
System:		PostgreSQL
Server:		postgres		(of the service in docker-compose.yml)
Username:	postgres
Password:	postgres
Database:	paperless

## Postgres quick checks (in the container):
List DBs / Tables:
``docker exec -it paperless_postgres psql -U postgres -d paperless -c "\l"``
``docker exec -it paperless_postgres psql -U postgres -d paperless -c "\dt"``
``docker compose exec postgres psql -U postgres -d paperless -c 'SELECT * FROM "Documents";'``
``docker compose exec postgres psql -U postgres -d paperless`` to get into the DB.
and then: ``SELECT * FROM "Documents";``

## Worker/REST log tails:
``docker logs -f paperless_ocrworker``
``docker logs -f paperless_rest``

## Quick check of logging for queue:
- publisher side (REST):
``docker compose logs -f rest | egrep "Published message|Failed to publish"``

- consumer side (OCR worker):
``docker compose logs -f ocrworker | egrep "Listening on queue|Received OCR job|Error while handling"``

## Chava's notes while working on ElasticSearch:
In order to implement this I changed/amended the following existing files:
- docker-compose.yml
- .env
- REST: Program.cs
- GenAiWorker.cs
- GenAiWorker: Program.cs
- OcrWorker.cs
- index.html
- styles.css
- app.js

## ElasticSearch curl:
``curl -s "http://localhost:8080/api/search?query=searchterm"``

