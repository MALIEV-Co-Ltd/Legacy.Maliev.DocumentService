# Document Application coverage applicability proposal

The scoped contract-only applicability decision is active through the committed
`document-contract-applicability-policy.json`, after full source and actual native
compiled-proof review of head6191a98/run37425501847. Application remains N/A with
a null percentage and numericalPassed=false; four-assembly numerical acceptance
remains false. The Application assembly has one interface, `IDocumentRenderer`,
with five abstract methods returning `byte[]` from the five owned Domain DTOs.
There is no production executable implementation in this assembly. Adding code
to manufacture a denominator would change production responsibilities.

The producer reads the fresh pre-instrumentation Release PE and portable PDB as
data. It never loads or executes the application or fixture assemblies. It
requires exactly the module and interface, five exact abstract instance methods
with zero RVA and no implementation flags, exact return/parameter signatures,
no fields, inherited/default/static/generated methods, additional types, native
headers, entry point, linked files or resources. The PDB GUID plus debug stamp
must match the DLL CodeView record and have no executable sequence points.
The actual AssemblyInformationalVersion must contain the exact candidate SHA.

Capture occurs after the mandatory fresh Release build and before any test
instrumentation. It freezes both source files, their exact tracked inventory,
DLL/PDB hashes, checkout SHA, run ID and attempt. Source bytes must match both
the reviewed fixed hashes and that candidate's Git objects. The inspector
re-reads every captured file and independently invokes the PE/PDB producer;
precomputed JSON cannot substitute for compiled evidence. Missing, altered,
mismatched or stale evidence fails closed.

N/A contract-only has a null percentage and numericalPassed=false. It is never
100% and never an 80% numerical pass. Any raw Application line, covered or
uncovered, contradicts applicability. API, Domain and Rendering keep separate
80% raw floors including generated lines and no exclusions. The actual 37
consumer HTTP cases and all 154 focused/248 full cases must pass with exact
execution mapping and all sixteen TRX counters reconciled. A failing API floor
remains a failure even if the Application contract proof succeeds.

Native controls compile five test-only mutations (concrete implementation,
default method, static method, extra type and resource). Their DLLs are inspected
without execution. Further controls reject absent/mismatched PDB, stale compiled
SHA, altered or missing source, wrong manifest SHA/run/attempt, invalid DLL with
an updated hash, and either covered or uncovered unexpected raw lines. Fixture
projects reference the already built Domain DLL and never rebuild production
assemblies or contribute production coverage. Their outputs stay in runner temp.

The metadata producer and capture retain their passive policyActive=false flags:
they describe the compiled surface, never infer policy approval or runtime deployment.
The independent applicability reader now emits a separate acceptance receipt only
after the exact committed policy, source inventory/hashes, current compiled DLL/PDB,
twenty native controls, both complete TRX inventories and all three executable floors
pass. Any raw Application line or source/compiled drift fails closed. The raw
four-assembly numerical result stays false. No denominator, exclusion or runtime
implementation is added to manufacture coverage; deployment remains unaccepted.

TRX admission requires exactly one direct ResultSummary with outcome Completed and
exactly one Counters in the whole document. Duplicate summaries/counters, orphan
counters, missing outcome and incomplete runs are rejected by passive controls.
