# Foundry Document Intelligence - Copilot Instructions

## Microsoft official documentation and implementation guidance

For all Microsoft and Azure technologies used in this repository, implementation
decisions must be based on current Microsoft official documentation.

Before implementing or modifying functionality that depends on Microsoft
services, SDKs, APIs, authentication, infrastructure, or recommended
architecture patterns:

1. Check the latest relevant Microsoft official documentation.
2. Confirm the currently recommended and supported implementation approach.
3. Confirm whether APIs, SDKs, features, and configuration options are GA,
   Preview, deprecated, or superseded.
4. Prefer current GA and supported approaches unless there is an explicit
   requirement to use a Preview feature.
5. Verify current SDK packages, API versions, authentication patterns, and
   service configuration before writing implementation code.
6. Prefer Managed Identity and Microsoft Entra ID authentication where supported
   and recommended by Microsoft.
7. Do not copy outdated implementation patterns from old samples, blog posts,
   Stack Overflow answers, or previous repository code without validating them
   against current Microsoft documentation.
8. When Microsoft documentation and an existing implementation conflict,
   identify the conflict and propose an update based on the current official
   guidance before changing the code.
9. If the recommended implementation cannot be confirmed from current Microsoft
   documentation, explicitly state the uncertainty instead of inventing or
   assuming an implementation.

Use the following source priority:

1. Microsoft Learn product documentation
2. Official Microsoft product documentation and architecture guidance
3. Official Azure SDK documentation
4. Official Microsoft GitHub repositories and samples
5. Other sources only as supplementary references

For Azure architecture decisions, also review relevant guidance from the
Microsoft Azure Architecture Center and Azure Well-Architected Framework when
applicable.

For this repository, this requirement is especially important for:

- Microsoft Foundry
- Azure Content Understanding
- Azure AI Search
- Azure OpenAI / Foundry Models
- Azure Storage
- Azure Functions
- Azure Container Apps and Container Apps Jobs
- Managed Identity and Microsoft Entra ID
- Azure SDK for .NET
- Bicep
- Application Insights and Azure Monitor
- GitHub Actions integration with Azure

Do not assume that an implementation pattern is still recommended simply
because it worked previously.

Before substantial implementation, summarize any important Microsoft guidance
that affects the design, including relevant limitations, Preview dependencies,
or breaking changes.

## Project purpose

This repository implements a reusable and configurable document intelligence
and search platform on Azure.

The platform must support multiple document datasets and customer scenarios
without rebuilding or forking the application for each customer.

Do not optimize the implementation for a specific customer, document dataset,
or demo scenario.

## Technology stack

Use the following technologies unless an architecture decision explicitly
changes them:

- .NET 10
- C#
- ASP.NET Core
- Blazor Web App
- Microsoft Foundry
- Azure Content Understanding
- Azure AI Search
- Azure Storage
- Azure OpenAI / Foundry Models where embeddings are required
- Azure Functions or Azure Container Apps Jobs for asynchronous processing
- Application Insights
- Managed Identity
- Bicep
- GitHub Actions
- xUnit

Prefer current supported Azure SDKs and current .NET APIs.

Do not introduce additional frameworks or Azure services unless there is a
clear requirement.

## Architecture principles

Use a single repository with separately deployable application responsibilities.

The two main responsibilities are:

1. Document ingestion and indexing
2. Document search application

Keep these responsibilities separated.

The intended processing pipeline is:

Raw Document
→ Content Understanding Result
→ Canonical Document Model
→ Search Chunks
→ Azure AI Search

Do not couple the application directly to raw Azure Content Understanding
responses.

Always transform provider-specific results into the canonical document model
before creating Azure AI Search documents.

## Workspace model

The platform must support multiple Workspaces.

A Workspace represents a document dataset or customer scenario.

Customer-specific configuration may include:

- document analysis configuration
- analyzer configuration
- metadata schema
- document classifications
- search filters
- facets
- evaluation datasets

Customer-specific concepts must not be hard-coded into the core application.

Examples of concepts that must not be hard-coded include:

- customer names
- game titles
- characters
- product names
- contract-specific fields

Prefer configuration-driven behavior.

## Data isolation

Always treat customer documents as isolated datasets.

Every document must belong to a Workspace.

Do not design queries or storage access patterns that could accidentally return
documents from another Workspace.

Infrastructure may use separate storage containers, search indexes, or Azure
resources when stronger isolation is required.

## Canonical document model

The canonical model should support common concepts including:

- DocumentId
- WorkspaceId
- Source
- Title
- Summary
- DocumentType
- Pages
- Content
- Tables
- Figures
- Metadata
- Tags
- Entities
- Evidence / SourceReferences

Keep information explicitly extracted from the source document distinguishable
from AI-generated or inferred information.

Preserve traceability to the original document and page wherever possible.

## Azure AI Search

The search application may support:

- keyword search
- vector search
- hybrid search
- semantic ranking
- filtering
- faceting

Search behavior must not contain special cases added only to improve demo
results.

Search quality must be measured through repeatable evaluation datasets.

## Security

Prefer Managed Identity for Azure authentication.

Do not store secrets, API keys, connection strings, customer data, or tokens
in source control.

Use Azure Key Vault only when secrets cannot be avoided.

Follow least-privilege principles.

## Implementation style

Favor:

- simple implementations
- explicit contracts
- dependency injection
- immutable domain models where practical
- async APIs for I/O
- nullable reference types
- clear error handling
- structured logging
- testable abstractions

Avoid:

- unnecessary abstractions
- speculative features
- customer-specific branching in core services
- hidden fallback behavior
- duplicated domain models
- silent exception handling

Do not create an interface merely because a class exists.

Create abstractions at meaningful architectural boundaries.

## Development workflow

Before implementing a substantial feature:

1. Read the relevant files under /docs.
2. Inspect the existing implementation and tests.
3. Identify the affected architectural boundary.
4. Identify the Microsoft products, SDKs, APIs, and Azure services involved.
5. Review the latest relevant Microsoft official documentation.
6. Confirm the currently recommended, supported implementation approach.
7. Check for GA / Preview / deprecated status and relevant service limitations.
8. Produce a short implementation plan.
9. Implement the smallest coherent change.
10. Add or update tests.
11. Build and run tests.
12. Report:
    - implementation summary
    - Microsoft guidance used for important design decisions
    - relevant official documentation references
    - validation results
    - assumptions
    - Preview dependencies
    - unresolved issues

Do not make unrelated refactoring changes while implementing a feature.

## Documentation

Architecture and domain decisions must remain consistent with:

- docs/requirements.md
- docs/architecture.md
- docs/data-model.md

If implementation requires changing an established architectural decision,
identify the conflict before changing the implementation.

## Current project phase

The repository is initially establishing architecture and contracts.

Prioritize:

1. repository structure
2. solution and project boundaries
3. domain abstractions
4. canonical document model
5. Workspace configuration model
6. infrastructure skeleton
7. automated tests

Do not implement unnecessary UI features or demo-specific optimizations before
the core architecture is established.

## Preview features

Do not use Preview features automatically just because they are newer.

A Preview feature may be used only when:

- it is required to satisfy a documented requirement, or
- it provides a meaningful capability that is not available in GA.

Before using a Preview feature:

1. Confirm its current status in Microsoft official documentation.
2. Identify the reason it is required.
3. Document its limitations and production-readiness implications.
4. Keep the dependency isolated where practical so it can be replaced later.

Prefer GA capabilities when they satisfy the requirement.