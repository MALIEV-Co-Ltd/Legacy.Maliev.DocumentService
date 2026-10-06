# Runnable document examples

The existing five PDF operations now expose synthetic PascalCase request examples
in their generated development OpenAPI reference. Their remarks explain the actual
render permission and PDF content type; the existing 200/400 statuses have concrete
descriptions. Examples contain no customer data or private source fixture values.
Committed v1 metadata incorrectly advertised application/pdf for every400 response,
while the accepted actual HTTP tests require rejection responses to be non-PDF.
The400 response metadata now explicitly declares ProblemDetails/application/problem+json;
the new actual HTTP cases require that media type and a status400 JSON body.
The first candidate7516df6 built without warnings/errors but all five new cases
failed because method-wide Produces(application/pdf) still appeared on400 alongside
the explicit ProblemDetails media type. The correction declares PDF only on the200
response and removes that method-wide declaration. Assertions and the File(...)
runtime response remain unchanged; no unchanged failing head is requeued.
Corrected head11bdb0e made all five served-example/PDF/400 cases pass, but its
void200 metadata omitted PDF content entirely and failed the existing metadata case.
The next correction declares the binary wire body as Stream, supported by the
pinned .NET10 schema generator, and requires string/binary PDF schemas in all five
new HTTP cases. Existing content-type and real PDF assertions stay unchanged.

Five new HTTP cases fetch each example from the served OpenAPI JSON and submit that
exact JSON to the normal authenticated Production route. They require real PDF bytes
containing the example marker, and the receipt case also checks its Thai amount text.
Each route's documented invalid-body400 outcome is checked through actual HTTP.
The existing production metadata404 and JWT/permission rejection controls remain.
No controller, renderer, serializer, auth or test-boundary replacement is added.

This improves the developer reference rather than claiming that historical private
XML already contained examples. It uses the existing .NET10 XML documentation
generator through normal AddOpenApi and HTTP; no helper reflection, generated-code
edits, extra endpoint, coverage exclusion or numerical waiver is introduced.
Reference: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/openapi-comments?view=aspnetcore-10.0

Head8e661fc passed154 focused/248 full cases and37 normal HTTP cases with zero
build warnings/errors. Its generated-inclusive API coverage350/438 remains below80%.
Receipt AmountPaid and Currency now have typed schema examples (5.99 and "THB")
for consumers that build requests from field schemas. The receipt HTTP case requires
the served examples to remain JSON number/string values, constructs its request from
those values, and verifies the normal authenticated PDF and Thai amount output.
This is additive developer documentation; no historical example parity is claimed.

Native validation of the field examples is pending. Expected focused count154 and full248 retain the
existing149/243 cases plus five new examples. The generated-inclusive API80% floor
remains unaccepted until actual raw readback proves it. Application numerical
coverage and the compiled-contract-only policy remain separate and inactive.
