; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 13.6.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ASPIREC4008 | AspireC4.EntityFrameworkCore | Info | A DbContext was found but no entity types could be discovered.
ASPIREC4009 | AspireC4.EntityFrameworkCore | Info | No primary key could be determined statically for an entity type.
ASPIREC4010 | AspireC4.EntityFrameworkCore | Info | Model configuration could not be interpreted by static analysis.
