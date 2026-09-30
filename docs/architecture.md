# Architecture

## System overview

The platform consists of two primary application responsibilities.

### Ingestion and Indexing

Azure Storage
→ Azure Content Understanding
→ Raw Analysis Result
→ Canonical Document Model
→ Search Chunk Generation
→ Embedding
→ Azure AI Search

### Search Application

User
→ Blazor Web App
→ Search Application Service
→ Azure AI Search
→ Search Results
→ Original Document / Page

## Application boundaries

The repository uses the following logical layers:

### Domain

Contains provider-independent domain models and business rules.

Must not depend on Azure SDKs.

### Application

Contains use cases and application orchestration.

Depends on Domain.

### Infrastructure

Contains Azure-specific implementations including:

- Azure Storage
- Azure Content Understanding
- Azure AI Search
- Foundry Models

### Ingestion

Hosts document ingestion and indexing workflows.

### Web

Hosts the Blazor search application.

## Dependency direction

Web
    ↓
Application
    ↓
Domain

Ingestion
    ↓
Application
    ↓
Domain

Infrastructure implements interfaces required by Application.

Domain must not reference Infrastructure.

## Workspace architecture

Workspace configuration defines customer or dataset-specific behavior.

Core application logic must remain customer independent.
