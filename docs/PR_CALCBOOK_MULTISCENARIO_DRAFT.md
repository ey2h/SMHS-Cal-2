Title: feat/calcbook-toc-multiscenario: multi-scenario TOC + diagram validation

Summary
- Extend Word CalcBook test harness to cover 11+ scenarios, validate TOC updates, diagrams, and captions, and emit a consolidated validation report for quick review.

What changed
- Harness: tests/CalcBookTestHarness/Program.cs
  - Added many new scenarios (S1..S11) with column/beam placeholders to trigger diagram rendering paths.
  - For each scenario, generate a dedicated CalcBook Word doc and collect per-scenario metrics:
    - bytes, image count, TOC presence, captions presence, and presence of Heading1/Heading2/Heading3 styles.
  - Accumulate per-scenario data into an in-memory report (SceneReport) and emit a Markdown summary at docs/validation_report.md.
  - Extended the in-code SceneReport model with fields: Heading1Present, Heading2Present, Heading3Present.
  - Implemented ContainsHeading(docBytes, style) checks by scanning word/document.xml for w:val="HeadingX" occurrences.

- Documentation: docs/next_steps.md
  - Updated to reflect the new multi-scenario outputs (S1..S11) and local validation steps.

- Validation: docs/validation_report.md
  - Auto-generated after running the harness to summarize per-scenario validation results.

How to verify locally
- Build and run extended harness:
  - dotnet build tests/CalcBookTestHarness/CalcBookTestHarness.csproj
  - dotnet run --project tests/CalcBookTestHarness/CalcBookTestHarness.csproj
- Review outputs in:
  - D:\Code\Glass\calcbook_test_output_s1.docx … calcbook_test_output_s11.docx
  - docs/validation_report.md

Notes
- The heading detection currently relies on primitive string matching against the OpenXML document.xml. It is a best-effort check; consider augmenting with a proper OpenXML parser-based heading extraction for more robust validation.
- If you want to prune the number of scenarios for CI, we can selectively keep a representative subset and progressively add back tests.

Next steps (optional)
- Integrate PR into a feature branch (e.g. feat/calcbook-toc-multiscenario-v5) and open a PR with the above description.
- Add more granular OpenXML assertions (structure-based) for heading texts and table of contents structure.
- If desired, add a small README in docs/ that explains how to run and interpret the validation report.
