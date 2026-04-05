# Release Definition of Done (DoD)

Use this checklist to close a release candidate and approve deployment.

Release Version:
Release Branch:
Release Date (UTC):
Release Manager:

## A) CI/CD Integrity

- [ ] Workflow file validated: .github/workflows/build.yml
- [ ] Unity build methods validated in BuildPipeline.cs
- [ ] Required scenes present and in Build Settings order
- [ ] Version bumped correctly (major.minor.build)
- [ ] Artifacts generated for Android AAB
- [ ] Artifacts generated for Android split APKs (ARMv7, ARM64)
- [ ] iOS Xcode project generated

## B) Distribution Readiness

- [ ] Android AAB uploaded to Google Play Internal Testing
- [ ] Internal track release status is completed
- [ ] iOS signed archive/IPA generated successfully
- [ ] TestFlight upload succeeded
- [ ] TestFlight build is visible and processing completed

## C) Functional and Quality Gates

- [ ] Smoke test passed on Android low-tier device
- [ ] Smoke test passed on Android mid/high-tier device
- [ ] Smoke test passed on iOS target device
- [ ] Login -> CharacterSelect -> Loading -> Main flow validated
- [ ] No blocker bugs in release scope
- [ ] Crash-free startup verified
- [ ] Performance baseline acceptable for release target

## D) Security and Compliance

- [ ] Secrets reviewed (no expired credentials)
- [ ] Signing cert/profile validity checked
- [ ] Service account permissions reviewed (least privilege)
- [ ] No sensitive data committed to repository
- [ ] Third-party licenses/compliance reviewed

## E) Observability and Rollback

- [ ] Build/run evidence links attached in monthly run log
- [ ] Known issues documented
- [ ] Rollback strategy documented and owner assigned
- [ ] Previous stable build artifact available
- [ ] Incident contact matrix confirmed

## F) Documentation and Handover

- [ ] Monthly CI log updated for all release runs
- [ ] Release notes drafted and reviewed
- [ ] QA sign-off recorded
- [ ] Product/PM sign-off recorded
- [ ] Engineering sign-off recorded

## Final Go/No-Go

Decision:
- [ ] GO
- [ ] NO-GO

Decision Timestamp (UTC):
Decision Owner:

## Evidence Links

- GitHub Actions runs:
- Google Play internal release:
- TestFlight build:
- QA report:
- Incident log entries:

## Signatures

- Release Manager:
- DevOps Owner:
- QA Owner:
- Android Owner:
- iOS Owner:
