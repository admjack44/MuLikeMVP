# CI/CD Validation Control Sheet

Use this sheet on every CI rollout to validate Unity -> Android -> iOS -> Distribution in a controlled sequence.

## 1) Validation Stages (must pass in order)

1. Stage A: Unity Base Build (license/auth only)
2. Stage B: Android Distribution (Google Play Internal)
3. Stage C: iOS Signing/Archive (IPA generation)
4. Stage D: TestFlight Upload (App Store Connect API)

Stop on first failing stage, fix, and rerun from that stage.

## 2) Run Register Template

| Run # | Date/Time UTC | Branch/Commit | Trigger (manual/push) | Stage | Job Name | Result (pass/fail/skipped) | Error Summary | Root Cause | Corrective Action | Owner | Re-run # | Final Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 001 | 2026-04-05 18:40 | main / abc1234 | manual | A | android-aab | pass | - | - | - | DevOps | 0 | pass |
| 002 | 2026-04-05 19:10 | main / def5678 | manual | B | android-aab | fail | Upload rejected | Package mismatch | Update ANDROID_PACKAGE_NAME secret | Android Lead | 1 | pass |
| 003 | 2026-04-05 20:05 | main / ghi9012 | manual | C | ios-xcode | fail | codesign error | Profile/cert mismatch | Re-export profile for bundle id | iOS Lead | 2 | pass |

## 3) Secret Validation Matrix

Mark each secret as configured and last validated.

| Secret | Configured (Y/N) | Last Checked (UTC) | Checked By | Notes |
|---|---|---|---|---|
| UNITY_LICENSE |  |  |  |  |
| UNITY_EMAIL |  |  |  |  |
| UNITY_PASSWORD |  |  |  |  |
| GOOGLE_PLAY_SERVICE_ACCOUNT_JSON |  |  |  |  |
| ANDROID_PACKAGE_NAME |  |  |  |  |
| APPLE_TEAM_ID |  |  |  |  |
| APPLE_SIGNING_CERT_BASE64 |  |  |  |  |
| APPLE_SIGNING_CERT_PASSWORD |  |  |  |  |
| APPLE_PROVISIONING_PROFILE_BASE64 |  |  |  |  |
| KEYCHAIN_PASSWORD |  |  |  |  |
| APPSTORE_API_KEY_ID |  |  |  |  |
| APPSTORE_ISSUER_ID |  |  |  |  |
| APPSTORE_API_PRIVATE_KEY |  |  |  |  |

## 4) Stage Pass Criteria

### Stage A: Unity Base Build
- Pass criteria:
  - Unity build jobs complete.
  - Store-upload steps may be skipped.
- Typical failures:
  - Invalid UNITY_LICENSE.
  - Wrong UNITY_EMAIL/UNITY_PASSWORD.

### Stage B: Android Distribution
- Pass criteria:
  - AAB artifact generated.
  - Upload to Google Play Internal succeeds.
- Typical failures:
  - Invalid service account JSON.
  - Missing Play Console permissions.
  - Wrong package id.

### Stage C: iOS Signing/Archive
- Pass criteria:
  - Xcode project generated.
  - IPA exported successfully.
- Typical failures:
  - p12 password mismatch.
  - Provisioning profile not matching bundle id/team.
  - Expired cert/profile.

### Stage D: TestFlight Upload
- Pass criteria:
  - IPA uploaded to TestFlight processing queue.
- Typical failures:
  - Wrong API key id/issuer.
  - Malformed private key content.

## 5) Incident Log Template

| Incident ID | Date/Time UTC | Stage | Impact | Evidence (job step/log line) | Root Cause | Fix Applied | Preventive Action | Owner | Closed (Y/N) |
|---|---|---|---|---|---|---|---|---|---|
| INC-001 |  |  |  |  |  |  |  |  |  |

## 6) Weekly Governance Checklist

- Rotate secrets scheduled (quarterly at minimum).
- Validate Unity license expiry date.
- Reconfirm Google Play service account permissions.
- Reconfirm Apple cert/profile expiry windows.
- Review failed runs trend and recurring causes.
- Confirm branch protections for workflow/config changes.

## 7) Fast Triage Flow

1. Was step skipped?
- Yes: secret missing/empty or condition not met.
- No: continue.

2. Did build step fail before upload?
- Unity/toolchain/signing issue.

3. Did upload step fail after artifact creation?
- Store auth/permissions/package mismatch issue.

4. Record run in table before applying fix.

## 8) References

- Workflow: .github/workflows/build.yml
- iOS export options: .github/workflows/ExportOptions.plist
- Setup guide: README_DEVOPS.md
