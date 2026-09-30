# Data Model

## Processing stages

The platform distinguishes between three data representations.

### Raw Analysis Result

Provider-specific output returned by Azure Content Understanding.

The raw result is preserved without modification.

### Canonical Document

Provider-independent representation of a document.

### Search Document

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

- SourceExplicit
- Extracted
- Inferred
- Generated

AI-generated or inferred metadata must not be represented as if it were
explicitly present in the source document.
