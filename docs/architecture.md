# Architecture

## 1. System overview

The platform is a reusable document intelligence and search platform designed
to support multiple customer datasets and document scenarios.

The platform consists of two primary deployable application responsibilities:

1. Document ingestion and indexing
2. Document search

Customer-specific behavior is provided through Workspace configuration and
must remain separated from shared application logic.

---

## 2. High-level processing flow

### Ingestion and Indexing

Azure Storage
→ Azure Content Understanding
→ Raw Analysis Result
→ Canonical Document Model
→ Search Chunk Generation
→ Embedding / Vectorization
→ Azure AI Search

### Search Application

User
→ Blazor Web App
→ Search Application Service
→ Azure AI Search
→ Search Results
→ Original Document / Page

The ingestion pipeline and search application are independently deployable.

Changes to the search UI must not require documents to be reprocessed unless
the search contract or index schema changes.

---

## 3. Data representations

The platform separates document data into three representations.

### Raw Analysis Result

The provider-specific result returned by Azure Content Understanding.

Characteristics:

- preserved without transformation
- used for troubleshooting and reprocessing
- must not be exposed as the core domain model

### Canonical Document Model

The provider-independent internal representation of analyzed documents.

Characteristics:

- independent from Azure Content Understanding SDK or REST types
- preserves source and evidence information
- represents documents, pages, tables, figures, regions, metadata, and entities
- distinguishes extracted, inferred, and generated information

The Canonical Document Model is the primary boundary between document analysis
and downstream indexing.

### Search Document

A representation optimized for Azure AI Search.

Characteristics:

- generated from the Canonical Document Model
- designed for retrieval, filtering, faceting, and ranking
- may represent a document, page, or region
- retains references to the Canonical Document and original source

See `docs/data-model.md` for detailed data contracts.

---

## 4. Application boundaries

The repository uses the following logical layers and applications.

### Domain

Contains provider-independent domain concepts and business rules.

Examples:

- CanonicalDocument
- CanonicalPage
- Evidence
- Workspace
- information origin
- processing state

Rules:

- must not depend on Azure SDKs
- must not depend on Infrastructure
- must not contain customer-specific domain logic
- should remain independently testable

### Application

Contains application use cases and orchestration contracts.

Examples:

- ingest document
- analyze document
- transform analysis result
- generate search chunks
- index document
- reprocess document
- execute search
- retrieve document details

Application depends on Domain.

Application defines abstractions for external capabilities required by use
cases where appropriate.

### Infrastructure

Contains implementations that depend on external platforms and Azure services.

Examples:

- Azure Storage
- Azure Content Understanding
- Azure AI Search
- Microsoft Foundry models
- telemetry integrations
- Azure identity integrations

Infrastructure implements contracts required by Application.

Azure SDK models must be translated at the Infrastructure boundary and must
not leak into Domain.

### Ingestion

Deployable application that hosts document ingestion and indexing workflows.

Responsibilities include:

- accepting or discovering documents to process
- coordinating processing stages
- recording processing status
- retries
- reprocessing
- invoking application use cases

The hosting technology must be selected through an architecture decision based
on current Microsoft guidance and workload requirements.

### Web

Deployable ASP.NET Core / Blazor Web App.

Responsibilities include:

- Workspace selection
- document discovery
- search
- filtering and faceting
- document and page detail
- source/evidence navigation

Web must not contain document ingestion or indexing logic.

---

## 5. Dependency direction

Logical dependency direction:

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

Infrastructure
    ↓
Application
    ↓
Domain

Domain has no dependencies on other solution projects.

Application must not depend on Infrastructure.

Web and Ingestion may reference Infrastructure at their composition roots to
register concrete implementations through dependency injection.

Infrastructure-specific types must not cross into Domain contracts.

---

## 6. Workspace architecture

A Workspace represents a document dataset or customer scenario.

Examples may include:

- a customer-provided dataset
- a generic demonstration dataset
- a future customer dataset

A Workspace can define configuration including:

- Workspace identifier
- display name
- document analysis configuration
- analyzer configuration
- metadata schema
- document classifications
- search filters
- search facets
- evaluation dataset

Core application logic must remain customer independent.

Adding a Workspace should primarily require configuration and data rather than
changes to shared application code.

### Workspace isolation

Every document and processing record must be associated with a Workspace.

The architecture must prevent unintended access across Workspaces.

Logical Workspace separation is mandatory.

Physical isolation, such as separate:

- Storage containers
- Search indexes
- Azure resources
- resource groups

may be used depending on customer security requirements.

The initial physical isolation strategy must be documented as an architecture
decision before customer data is onboarded.

---

## 7. Storage architecture

Azure Storage is used for durable document and processing artifacts.

The logical storage model should distinguish at least:

- original documents
- raw analysis results
- canonical documents or serialized processing artifacts when persisted
- derived assets such as thumbnails when required

A conceptual layout may be:

workspaces/
  {workspaceId}/
    source/
    analysis/
    processed/
    derived/

The exact container strategy must account for Workspace isolation and customer
security requirements.

Storage paths must not encode customer-specific application logic.

---

## 8. Search architecture

Azure AI Search is the retrieval layer.

Search Documents are generated from the Canonical Document Model.

The search layer may support:

- keyword search
- vector search
- hybrid search
- semantic ranking
- filtering
- faceting

Search logic must use common search abstractions.

Customer-specific query rewriting or hard-coded ranking rules must not be added
to shared search services.

### Search unit

The initial supported retrieval unit is page-level.

Region-level Search Documents may be created when document structure or search
quality requires finer-grained retrieval.

Every Search Document must retain enough information to identify:

- Workspace
- source document
- page
- region when applicable
- processing/schema version where required

### Vectorization

The exact vectorization implementation is an architecture decision.

The implementation must evaluate current Microsoft-recommended approaches,
including:

- application-controlled chunking / embedding / push indexing
- Azure AI Search integrated vectorization where applicable

The selected approach must preserve:

- Canonical Document Model
- traceability
- repeatable evaluation
- re-indexing
- Workspace configurability

Do not select an approach solely because it reduces initial implementation
effort.

---

## 9. Processing and reprocessing

Document processing must be modeled as an explicit pipeline.

Conceptual stages:

Received
→ Analyzing
→ Transforming
→ Chunking
→ Indexing
→ Completed

Failures must be observable and recoverable.

A failed document must be reprocessable without requiring unrelated successful
documents to be processed again.

### Idempotency

Processing and indexing operations must be idempotent where practical.

Stable identifiers must be used for documents and generated Search Documents
so that reprocessing updates existing records instead of creating unintended
duplicates.

### Versioning

The architecture should support identifying the versions of processing inputs
that materially affect derived data, including where applicable:

- analyzer configuration
- canonical transformation
- chunking strategy
- embedding model
- search schema

Version details should be sufficient to determine when reprocessing or
re-indexing is required.

---

## 10. Configuration architecture

Environment configuration and Workspace configuration are separate concepts.

### Environment configuration

Examples:

- Azure endpoints
- resource names
- region
- application settings
- feature availability

Environment-specific values must not be hard-coded.

### Workspace configuration

Examples:

- metadata definitions
- analyzer selection
- classifications
- filters
- facets
- evaluation dataset

Workspace-specific configuration must not require modification of shared domain
code for normal onboarding scenarios.

Secrets must not be stored in Workspace configuration.

---

## 11. Security and identity

Microsoft Entra ID and Managed Identity should be used for Azure service
authentication where supported.

The Web and Ingestion applications must use separate identities when they have
different permissions.

Conceptually:

Web
→ read/query permissions

Ingestion
→ document processing and index write permissions

Permissions must follow least-privilege principles.

Secrets and service keys must not be committed to source control.

Authentication and RBAC implementation must be validated against current
Microsoft official documentation before deployment.

---

## 12. Observability

Application Insights / Azure Monitor are used for application and processing
observability.

Telemetry should allow operators to correlate processing activity using
identifiers such as:

- WorkspaceId
- DocumentId
- processing operation / correlation ID

The system should provide sufficient telemetry to identify failures in:

- document ingestion
- Content Understanding analysis
- canonical transformation
- search chunk generation
- embedding/vectorization
- Azure AI Search indexing
- search requests

Customer document content must not be unnecessarily written to telemetry.

---

## 13. Evaluation architecture

Evaluation is treated separately from production search behavior.

Evaluation datasets may be Workspace-specific.

Evaluation must be repeatable and must use the same production search pipeline
being evaluated.

Production code must not contain special-case logic whose sole purpose is to
improve specific evaluation queries.

Evaluation results should make it possible to compare search behavior across
implementation or configuration changes.

---

## 14. Infrastructure architecture

Azure infrastructure is defined using Bicep.

Infrastructure should be modularized by meaningful resource responsibility
rather than creating one module per resource without reason.

Environment-specific configuration should be supplied through parameters.

Infrastructure must be deployable into a clean Azure environment.

Microsoft official documentation and current supported Azure resource API
versions must be reviewed before implementation.

---

## 15. Deployment model

The repository contains one codebase with multiple independently deployable
applications.

Conceptually:

Repository
├── Web application
└── Ingestion application

Each application can have an independent deployment workflow.

Infrastructure deployment is managed independently from normal application
deployment where practical.

GitHub Actions is used for CI/CD.

Azure deployment authentication must avoid long-lived credentials and follow
current Microsoft-recommended GitHub-to-Azure authentication guidance.

---

## 16. Architecture decisions not yet finalized

The following decisions must not be made implicitly during implementation.

They should be researched against current Microsoft official guidance and
recorded before implementation.

### ADR-001 Ingestion hosting

Options under consideration:

- Azure Functions
- Azure Container Apps Jobs

Decision criteria include:

- execution model
- document processing duration
- retry requirements
- concurrency
- operational complexity
- cost
- Content Understanding processing characteristics

### ADR-002 Search indexing and vectorization

Options under consideration:

- application-controlled Search Document generation and push indexing
- Azure AI Search integrated vectorization where compatible

### ADR-003 Workspace search isolation

Options may include:

- shared index with strict Workspace filtering
- index per Workspace
- index per customer/environment

Security and operational requirements must drive this decision.

### ADR-004 Workspace storage isolation

Options may include:

- shared Storage Account with isolated containers/prefixes
- separate Storage Account per environment or customer

### ADR-005 Content Understanding integration

Confirm:

- current GA API
- supported .NET SDK status
- REST vs SDK implementation
- required API version
- regional availability

Preview dependencies must be explicitly documented.

---

## 17. Microsoft technology guidance

Implementation involving Microsoft services must be validated against current
Microsoft official documentation before code is written.

Priority should be given to:

1. Microsoft Learn
2. official Azure architecture guidance
3. official Azure SDK documentation
4. official Microsoft GitHub repositories and samples

Prefer current GA and supported capabilities when they satisfy the
requirements.

Preview functionality must only be introduced deliberately and its dependency
must be documented.