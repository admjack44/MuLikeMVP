# DevOps Master Index

This index is the single entry point for CI/CD operations, monthly run logs, and release closure criteria.

## Core Documents

- Setup Guide: [README_DEVOPS.md](../../../../README_DEVOPS.md)
- Validation Sheet Template: [CI_VALIDATION_LOG_TEMPLATE.md](../../../../CI_VALIDATION_LOG_TEMPLATE.md)
- Monthly Run Log Template: [CI_Run_Log_YYYY_MM.md](CI_Run_Log_YYYY_MM.md)
- Release Definition of Done: [RELEASE_DEFINITION_OF_DONE.md](RELEASE_DEFINITION_OF_DONE.md)

## Monthly Run Logs

### 2026

- April: [CI_Run_Log_2026_04.md](CI_Run_Log_2026_04.md)
- May: pending
- June: pending
- July: pending
- August: pending
- September: pending
- October: pending
- November: pending
- December: pending

## Quick Navigation by Workflow Area

- Build pipeline script: [BuildPipeline.cs](../../Editor/BuildPipeline.cs)
- GitHub Actions workflow: [build.yml](../../../../.github/workflows/build.yml)
- iOS export options: [ExportOptions.plist](../../../../.github/workflows/ExportOptions.plist)

## Operational Routine (Recommended)

1. Start day: open current monthly log and define daily CI goal.
2. For every run: register run row before changes.
3. On failure: capture root cause and corrective action.
4. End day: complete Day Closure section.
5. Release close: complete Definition of Done checklist and archive evidence links.

## Ownership Model

- DevOps Owner: workflow integrity, runner health, secrets governance.
- Android Owner: package, signing, Play Internal validation.
- iOS Owner: signing, archive, TestFlight processing.
- QA/Release Owner: traceability, evidence, release Go/No-Go.
