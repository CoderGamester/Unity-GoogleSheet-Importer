# GameLovers DataImporter Tests — Agent Guide

This guide adds test-only rules to the package and host guides.

## Shared test rules

<!-- BEGIN SHARED TEST RULES -->
### Admission

A new test is admitted only when all six answers are yes:

| Check | Requirement |
|---|---|
| **A1 Defect** | Name the production file/symbol and the incorrect behavior in one sentence. “It could break” is not a defect. |
| **A2 Red** | Name a plausible production edit that should make the assertion fail. Prefer one line/branch; shared-path integration mutations are allowed when isolation is dishonest. |
| **A3 Package-owned** | Every assertion must read behavior this package computes, not C# defaults, fresh-object non-nullness, or Unity guarantees. |
| **A4 Cheapest** | Use the cheapest honest tier: a test case before a new test, EditMode before PlayMode, and an existing fixture before a new fixture. |
| **A5 Unique** | Grep the symbol, derived/wrapper types, and paired setup fields. Do not add a test already reddened by the same narrow defect. |
| **A6 Environment** | Control ambient renderer, Addressables, sample, static, and project state, or branch the expectation on the state actually observed. |

Two additional rejects apply:

- **D1 Tautology:** a lone `DoesNotThrow`, freshly-created non-null assertion, language default, or input-derived substring match pins no package behavior unless used by a named harness sentinel.
- **D2 Name/body mismatch:** deleting or bypassing the behavior promised by the test name must not leave the test green. Strengthen the assertion or rename the test to its actual claim.

Fixtures under `Smoke/` are exempt from A1/A2 and may assert construction/bootstrap viability. The exemption is directory-scoped, not permission to use smoke assertions in Unit or Integration fixtures.

### Revert and Confirm Red (RCR)

Every new or strengthened behavioral test must be observed failing once against a plausible production mutation before commit:

1. Run the new test against normal production code and observe GREEN. The harness prints `baseline green: N test cases`; that line is the fixture count, not a reason to run it again.
2. Preserve the exact working patch or use an isolated worktree; then apply the A2 mutation. Never restore a dirty file from `HEAD`.
3. Run the smallest attributable filter. RED must come from the intended assertion with a diagnostic failure, not a compile error or unrelated `NullReferenceException`.
4. Restore the saved production state, confirm the mutation is gone without losing other edits, and observe GREEN again as one re-run of the same filtered probe.
5. Record the observation on the test using `file + symbol`, never a line number.
6. Correcting an existing `ADMIT`/`RCR` line is itself a claim about production: read the symbol it names, re-run the mutation when the claim is about RED, and run `python3 Tools/rcr-anchor-check.py` before committing.

Use this compact form, targeting four lines and never exceeding six:

```csharp
[Test]
// ADMIT: <owned defect naming production file and symbol>
// RCR: <file> <symbol> — <mutation> → RED (<assertion failure>). <YYYY-MM-DD>
public void Method_Condition_ExpectedResult()
```

Do not narrate investigation history in the test. A nearby mutation that looked valid but stayed green may be recorded when that negative result prevents repeated work.

When a test resists an isolated mutation, classify it before acting:

| Verdict | Meaning | Action |
|---|---|---|
| **A3 reject** | No package production behavior participates. | Delete the test. |
| **A5 duplicate** | The same narrow mutation already belongs to a sibling. | Delete it and name the surviving sibling in review/commit context. |
| **D2 overclaim** | The mutation implied by the name leaves the body green. | Strengthen or rename. |
| **UNFALSIFIABLE** | Real package behavior is double-guarded or cannot be broken by a safe isolated edit. | Keep only after attempted mutations are recorded with the specific reason. |
| **SHARED-PATH** | A broader mutation reddens this legitimate integration path together with siblings. | Keep, recording the observed mutation and blast radius. |

Unannotated tests have three possible histories: observed RED with lost write-back, collateral RED under another test's mutation, or never probed. Check `.test-all/rcr/` before probing and never write prepared annotation text without matching observed evidence. Mutation records stay under `.test-all/rcr/`, not `/tmp`.

Benchmarks use the inverted check: removing the workload from the measured body must materially change the result. Run the actual test assembly; a plain Unity open does not compile assemblies constrained by `UNITY_INCLUDE_TESTS`.
<!-- END SHARED TEST RULES -->

## Current suite

- The package has one Editor-only test assembly with four fixtures: `CsvParserTest` (CSV reading and writing, cell parsing, column validation), `XlsxReaderTest` (Excel worksheets), `DataImporterEditorUtilsTest` (Google Sheet URL rewriting, `Run` results, the report format, command-line arguments, and key-path resolution), and `GoogleServiceAccountTest` (service-account key reading and the signed assertion).
- `GoogleServiceAccountTest` generates its RSA key in memory and encodes it as PKCS#8 itself. Never commit a private key or a service-account JSON file, even a throwaway one: the repository is public and secret scanners flag them.
- The test assembly references the package Editor assembly, where the parser and readers live, plus the runtime and GameData assemblies and Newtonsoft.Json. `InternalsVisibleTo` exposes the command-line helpers to it. Do not claim coverage of downloads, importer discovery, the window, or `AssetDatabase` writes from this suite.
- `XlsxReaderTest` writes each workbook it needs into the temp folder from inline XML and deletes it in teardown. Do not add binary workbook fixtures.
- Test importers are NSubstitute substitutes of `IDataImporter`. Never declare a concrete `IDataImporter` class in the test assembly: importer discovery would find it, and it would appear in the window and in `ImportAll`.

## Adding coverage

- Add parser cases to `CsvParserTest` and reader cases to `XlsxReaderTest` unless a genuinely independent subject justifies another fixture.
- Prefer table-driven cases for delimiters, scalar types, nullable values, collections, dictionaries, key/value structs, ignored fields, and fallback JSON.
- Assert populated field values and failure behavior, not merely successful construction.
- A test that depends on the regional settings sets `CultureInfo.CurrentCulture` itself and restores it in a `finally`.
- Asset writes, pulls, and the window require a real import of the Data Import sample and inspection of the generated assets and CSV files.

## Verification

- Run the Editor test assembly after every parser, reader, or URL-rewriting change.
- Update this guide only when the test assembly, fixture layout, or stable test convention changes.
