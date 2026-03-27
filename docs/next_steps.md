# Next Steps for Word CalcBook Enhancements

- Summary of what was done so far
  - TOC: Added a Word TOC domain patch and Heading1/Heading2/Heading3 styling, with an update-domain hint text at the end of the TOC page. Harness now supports generating a sample document and updating TOC via F9 in Word.
  - Diagrams: Implemented placeholder and actual diagram rendering paths for column/beam sections. Added a placeholder diagram generator to keep sample docs visually complete.
  - Local validation harness: Extended test harness to exercise the full CalcBookWordService path, including column/beam data paths.
- Output: Extended harness run produced multiple documents for different scenarios:
  - D:\Code\Glass\calcbook_test_output_s1.docx
  - D:\Code\Glass\calcbook_test_output_s2.docx
  - D:\Code\Glass\calcbook_test_output_s3.docx
  - D:\Code\Glass\calcbook_test_output_s4.docx
  - D:\Code\Glass\calcbook_test_output_s5.docx
  - D:\Code\Glass\calcbook_test_output_s6.docx
  - D:\Code\Glass\calcbook_test_output_s7.docx
  - D:\Code\Glass\calcbook_test_output_s8.docx
  - D:\Code\Glass\calcbook_test_output_s9.docx
  - D:\Code\Glass\calcbook_test_output_s10.docx
  - D:\Code\Glass\calcbook_test_output_s11.docx
  All include TOC-ready content and diagram placeholders or actual diagrams when configured.

- How to reproduce the current state locally
  1) Build the harness
     - dotnet build tests/CalcBookTestHarness/CalcBookTestHarness.csproj
  2) Run the harness
     - dotnet run --project tests/CalcBookTestHarness/CalcBookTestHarness.csproj
  3) Open the generated document
     - D:\Code\Glass\calcbook_test_output.docx
  4) Validate in Word
     - Ctrl+A -> F9 to update TOC; verify heading levels and the updated TOC content

- What to validate next
  - B: TOC final stability across environments/Word versions
    - Ensure Heading1/Heading2/Heading3 are correctly picked up by Word and appear in the TOC
    - Confirm the update-domain hint remains visible and non-intrusive
  - C: Diagram integration and typography
    - Replace placeholder diagrams with actual structural diagrams (four types):
      1) 受力简图
      2) 弯矩图
      3) 剪力图
      4) 挠度图
    - Ensure captions are clear and centered; font and spacing match sample

- How to run extended validation with more scenarios
  - The harness currently supports a rich input model (GlassCalcBookInput). To exercise more paths:
    - Set input.Column and input.Beam with representative values to generate actual diagrams in the document
    - Re-run the harness to generate a new doc and verify that images appear in the expected sections

- CI / PR plan (draft)
  - Branch: feat/calcbook-toc-diagrams
  - PR title: Enhance Word CalcBook: automatic TOC generation and diagram integration
  - PR body: Include notes about TOC domain injection, Heading1/2/3, diagram insertion, and a test harness for local verification
  - Include: instructions to run the harness locally and a link to usage in docs/next_steps.md

- Risks and considerations
  - TOC behavior may differ across Word versions; validate on several common versions
  - Diagram rendering relies on Windows-friendly drawing APIs; ensure tests remain resilient in non-Windows environments
  - Keep placeholders separate from actual content to prevent confusion in final deliverables

- Quick verification checklist
  - [ ] Harness builds cleanly
  - [ ] Document generation completes and outputs to calcbook_test_output.docx
  - [ ] TOC can be updated in Word (Ctrl+A, F9)
  - [ ] Diagram placeholders render correctly; captions present
  - [ ] Optional: Column/Beam paths render diagrams when enabled

- Next actions (choose one)
  1) Extend harness further with more realistic input scenarios and add automated assertions in code
  2) Prepare a PR draft including A/B/C changes and a minimal test harness for CI
  3) Proceed to implement full C (real diagrams) in a separate milestone

- Contact and ownership
  - If you want, I can continue by:
    - Adding more harness scenarios and simple validation checks
    - Drafting the PR and branch structure for you to review
    - Automating a lightweight OpenXML validation pass in addition to Word manual checks

Notes:
- This document assumes a Windows development environment for diagram rendering (due to drawing APIs). Adjustments may be needed for cross-platform CI.
