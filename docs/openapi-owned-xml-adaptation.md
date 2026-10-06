# Owned OpenAPI XML documentation registration

The source checkpoint `135e526d0dab85c415b3afdcefd7b70fe2c82e2f` emits XML
documentation in both `Maliev.PdfService.Api` and `Maliev.PdfService.Entities`
for all four Debug/Release production configurations. API `ServiceExtensions`
passes its own executing-assembly XML file to Swagger. Its five controller
methods declare 200/400 responses and the exact summaries restored here.
This does not claim that old Swagger loaded the separate Entities XML file.

The extracted Domain retains source comments including `Receipt Model.` and
`Gets or sets the amount paid.`, but previously did not emit XML. The extracted
API also did not emit XML and omitted the source method summaries and declared
400 metadata. Enabling XML on those existing projects exposes these preserved
source descriptions without changing their DTO fields, nullability or JSON.

The pinned shared registration configures versioning, title and document
transforms through an Asp.Versioning builder wrapper. It does not provide an
owned, literal `IServiceCollection.AddOpenApi` call for this API's source
generator to intercept. Adding `builder.Services.AddOpenApi("v1")` retains the
shared registration and its development-only Scalar/OpenAPI mapping while
registering the owned XML transformers through the supported .NET 10 path.
Microsoft documents that standard literal overloads are intercepted and
referenced projects must emit XML:
[OpenAPI XML documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/openapi-comments?view=aspnetcore-10.0).

The API directly references the existing Domain project and the same OpenAPI
10.0.12 package already used by pinned ServiceDefaults. The package's
`GenerateAdditionalXmlFilesForOpenApi` build target collects emitted XML from
project references after reference resolution. Receiving the assembly and
generator transitively through ServiceDefaults does not import that build target.
Candidates d7e8bdb and 90ee9ac each built with zero warnings/errors and passed
148/149 focused cases, including route summaries, while the Receipt schema
description remained absent. Adding the direct Domain reference alone did not
resolve discovery; the direct package imports the required XML collection target.
The DTO assembly and exact schema-description assertions remain unchanged; no
runtime transformer substitutes the comments.

The existing real Development HTTP case checks all five exact source summaries,
declared 400 responses, retained PDF success content, the receipt type description
and AmountPaid property description. Production still requires 404 for metadata,
and all 32 prior real runtime HTTP cases remain in the 149 focused/243 full suite.
No controller, authentication service, renderer or metadata transformer is
replaced by a test fixture. No generated helper is invoked directly or reflected.

Native build, warnings, schema behavior and generated-inclusive raw coverage
remain mandatory observations. This adapter does not promise an 80% API floor,
exclude generated lines, activate the Application applicability proposal, change
the existing numerical policy or authorize a merge/deployment while coverage fails.
