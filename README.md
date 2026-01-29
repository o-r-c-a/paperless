# Paperless

**Paperless** is a modern, containerized document management system designed to streamline the storage, indexing, and retrieval of digital documents. Built with **.NET 9**, it leverages **Optical Character Recognition (OCR)** and **Generative AI (Google Gemini)** to automatically extract content and generate concise summaries for every uploaded document.

## Key Features

* **Document Management**: Securely upload and store PDF and image documents via MinIO Object Storage.
* **AI-Powered Summarization**: Automatically generates intelligent summaries of document content using the Google Gemini API.
* **Optical Character Recognition (OCR)**: Extracts text from images and PDFs (supports English and German) for full-text indexing.
* **Advanced Search**: Search documents by title, raw content, extracted text, or tags using Elasticsearch.
* **Tagging System**: Organize documents with custom tags (add, remove, and edit tags).
* **Event-Driven Architecture**: Utilizes RabbitMQ for asynchronous processing of OCR, Indexing, and AI tasks to ensure high performance.
* **Clean Architecture**: Built using Domain-Driven Design (DDD) and CQRS patterns.

## Technology Stack

### Backend & Infrastructure

* **Framework**: .NET 9
* **Database**: PostgreSQL 15
* **Messaging**: RabbitMQ
* **Search Engine**: Elasticsearch 8.15
* **Object Storage**: MinIO (S3 Compatible)
* **AI Integration**: Google Gemini API
* **Containerization**: Docker & Docker Compose

### Microservices / Workers

* **Paperless.Rest**: Main API Gateway handling client requests.
* **Paperless.OcrWorker**: Processes documents to extract text using Tesseract.
* **Paperless.GenAiWorker**: Interfaces with Gemini to generate content summaries.
* **Paperless.IndexWorker**: Synchronizes document data with Elasticsearch.

### Frontend

* **Web**: Lightweight HTML/JS interface served via Nginx.

## 🏗 Architecture Overview

The application follows a **Clean Architecture** approach with **CQRS** implemented via MediatR.

1. **Upload**: User uploads a file via the REST API.
2. **Storage**: File is stored in MinIO; metadata is saved to PostgreSQL.
3. **Processing**: A message is published to RabbitMQ.
* **OCR Worker** consumes the message, extracts text, and updates the index.
* **GenAI Worker** analyzes the text and generates a summary.
* **Index Worker** ensures Elasticsearch is updated for searchability.



## Prerequisites

* [Docker](https://www.docker.com/) and [Docker Compose](https://docs.docker.com/compose/)
* A Google Gemini API Key (Get one [here](https://aistudio.google.com/))

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/paperless.git
cd paperless

```

### 2. Configure Environment Variables

Copy the template configuration file to create your `.env` file:

```bash
cp gem_readme/.env.template .env

```

Open `.env` and **you must set your Gemini API Key**:

```ini
GEMINI_API_KEY=your_actual_api_key_here

```

*Note: You can leave the default database and RabbitMQ credentials for local development.*

### 3. Run with Docker Compose

Build and start all services:

```bash
docker-compose -f gem_readme/docker-compose.yml up -d --build

```

Wait a few moments for the database, elasticsearch, and workers to initialize.

## Usage

### Web Interface

Access the frontend application at:

* **http://localhost:80**

### API Documentation (Swagger)

For direct API interaction and testing, visit the Swagger UI:

* **http://localhost:8080/swagger**

### Infrastructure Consoles

* **MinIO Console** (Storage): `http://localhost:9001` (User/Pass: `minioadmin`)
* **RabbitMQ Management** (Queues): `http://localhost:15672` (User/Pass: `paperless`)
* **Adminer** (Database): `http://localhost:9091`

## 📂 Project Structure

```
├── Paperless.Application    # Business logic, Commands, Queries, Interfaces
├── Paperless.Contracts      # Shared DTOs and Message definitions
├── Paperless.Domain         # Domain Entities and Value Objects
├── Paperless.Infrastructure # EF Core, External Services (MinIO, Elastic)
├── Paperless.Rest           # API Controllers
├── Paperless.Web            # Frontend assets
├── Paperless.OcrWorker      # Background worker for OCR
├── Paperless.GenAiWorker    # Background worker for AI Summaries
├── Paperless.IndexWorker    # Background worker for Search Indexing
└── docker-compose.yml       # Orchestration

```

## 📄 License

[MIT License](https://www.google.com/search?q=LICENSE)