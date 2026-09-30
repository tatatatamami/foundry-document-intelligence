# Data Model

## Processing stages

The platform distinguishes between three data representations and an explicit
enrichment stage.

### Raw Analysis Result

Provider-specific output returned by Azure Content Understanding.

The raw result is preserved without modification.

### Canonical Document

Provider-independent representation of a document.

### Application-controlled AI Enrichment

Structured enrichment updates the existing Canonical Document metadata using
the Workspace taxonomy. It does not create a second document model.

### Search Chunk

Representation optimized for Azure AI Search.

## Canonical Document

Initial conceptual model:

CanonicalDocument

- DocumentId
- WorkspaceId
- Source
- Title
- Summary
- DocumentType
- Pages
- Metadata
- Tags
- Entities

CanonicalPage

- PageId
- PageNumber
- Content
- Tables
- Figures
- Regions
- Evidence

Evidence

- SourceDocumentId
- PageNumber
- BoundingRegion
- ExtractedValue
- Confidence
- Origin

## Information origin

Information should distinguish between:

- SystemAssigned
- SourceExplicit
- Extracted
- Inferred
- Generated

AI-generated or inferred metadata must not be represented as if it were
explicitly present in the source document.

`WorkspaceId`, `DocumentId`, and `PageId` are identities and do not have an
information origin. `SystemAssigned` applies only to metadata values assigned
by the platform, such as source file name, source path, and collection type.

The initial enrichment metadata uses these origins:

- `documentDate`: `Extracted`
- `documentCategory`: `Inferred`
- `contentIndexes`: `Inferred`
- `semanticTags`: `Inferred`
- `developmentPhase`: `Inferred`
- `summary`: `Generated`

## Search Chunk

The initial search unit is one Canonical Page. Each Search Chunk contains:

- stable chunk, Workspace, document, and page identities
- page number and source metadata
- document-level classification, tags, date, phase, and summary
- page OCR content
- an application-generated content vector

Document-level metadata is projected onto every related page chunk so a single
Azure AI Search query can apply Workspace isolation and taxonomy filters while
returning page-level results.
