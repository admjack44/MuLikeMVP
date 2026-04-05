# DevOps Setup Guide (Unity Mobile CI/CD)

This guide explains how to configure CI/CD for this Unity project using GitHub Actions.
It is aligned with the workflow file at .github/workflows/build.yml.

## 1) What this pipeline does

The workflow supports:
- Android AAB build (for Google Play upload)
- Android split APK builds by architecture (ARMv7, ARM64)
- iOS Xcode project build
- Optional upload to:
- Google Play Internal Testing
- TestFlight

It also includes a Unity Cloud Build alternative note.

## 2) Required GitHub Secrets

Create these in GitHub repository settings:
- Settings > Secrets and variables > Actions > New repository secret

### A) Unity license/auth secrets

| Secret Name | Example Value | Where to get it | Required For |
|---|---|---|---|
| UNITY_LICENSE | Long Unity license text/XML content | Unity licensing activation file for CI | All Unity builds |
| UNITY_EMAIL | devops@yourstudio.com | Unity account used for license | All Unity builds |
| UNITY_PASSWORD | ******** | Password of that Unity account | All Unity builds |

### B) Android / Google Play secrets

| Secret Name | Example Value | Where to get it | Required For |
|---|---|---|---|
| GOOGLE_PLAY_SERVICE_ACCOUNT_JSON | {"type":"service_account",...} | Google Cloud service account key JSON with Play Console access | Upload to Internal Testing |
| ANDROID_PACKAGE_NAME | com.yourstudio.mulikemvp | Android applicationId/package id | Upload to Internal Testing |

### C) Apple signing and App Store Connect secrets

| Secret Name | Example Value | Where to get it | Required For |
|---|---|---|---|
| APPLE_TEAM_ID | A1B2C3D4E5 | Apple Developer account (Membership details) | iOS archive/sign |
| APPLE_SIGNING_CERT_BASE64 | MII... (base64) | Export iOS distribution certificate (.p12), then base64 encode | iOS archive/sign |
| APPLE_SIGNING_CERT_PASSWORD | ******** | Password used when exporting .p12 | iOS archive/sign |
| APPLE_PROVISIONING_PROFILE_BASE64 | LS0t... (base64) | Download .mobileprovision, then base64 encode | iOS archive/sign |
| KEYCHAIN_PASSWORD | ******** | Custom password you set for temporary CI keychain | iOS archive/sign |
| APPSTORE_API_KEY_ID | ABCDE12345 | App Store Connect API key | TestFlight upload |
| APPSTORE_ISSUER_ID | 00000000-0000-0000-0000-000000000000 | App Store Connect API key issuer | TestFlight upload |
| APPSTORE_API_PRIVATE_KEY | -----BEGIN PRIVATE KEY-----... | Contents of .p8 API key | TestFlight upload |

## 3) How to create each secret (step-by-step)

### 3.1 Unity license/auth

1. Ensure you have a Unity account for CI use.
2. Obtain CI-compatible license content (per your Unity license type).
3. In GitHub, create:
- UNITY_LICENSE
- UNITY_EMAIL
- UNITY_PASSWORD

Security recommendation:
- Use a dedicated service account for CI.
- Do not reuse personal Unity credentials.

### 3.2 Google Play service account JSON

1. In Google Cloud, create/select project linked to Play Console.
2. Create Service Account and generate JSON key.
3. In Play Console, grant that service account release access.
4. Open JSON file and copy full contents.
5. Add secret GOOGLE_PLAY_SERVICE_ACCOUNT_JSON.
6. Add secret ANDROID_PACKAGE_NAME with exact package id.

Common error:
- Package name mismatch between build and Play listing.

### 3.3 Apple signing material (certificate + profile)

1. Export your iOS distribution certificate from Keychain as .p12 with password.
2. Download matching provisioning profile from Apple Developer portal.
3. Base64 encode both files.
4. Add secrets:
- APPLE_SIGNING_CERT_BASE64
- APPLE_SIGNING_CERT_PASSWORD
- APPLE_PROVISIONING_PROFILE_BASE64
- APPLE_TEAM_ID
- KEYCHAIN_PASSWORD

Base64 examples:
- macOS/Linux:
  - base64 -i cert.p12 | pbcopy
  - base64 -i profile.mobileprovision | pbcopy
- Windows PowerShell:
  - [Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.p12"))
  - [Convert]::ToBase64String([IO.File]::ReadAllBytes("profile.mobileprovision"))

### 3.4 App Store Connect API key for TestFlight

1. In App Store Connect, create API key (Users and Access > Keys).
2. Save Key ID, Issuer ID, and download the .p8 file.
3. Add secrets:
- APPSTORE_API_KEY_ID
- APPSTORE_ISSUER_ID
- APPSTORE_API_PRIVATE_KEY (paste full .p8 content)

## 4) Recommended GitHub Environments

Create environments:
- development
- production

Assign secrets by environment if you want stricter controls:
- Internal testing keys in development
- Production/TestFlight release keys in production

Enable reviewers for production deployments.

## 5) Running builds

### Manual run

1. Go to Actions tab.
2. Select "Unity Mobile CI".
3. Click "Run workflow".

### Automatic run

Workflow is currently triggered on push to:
- main
- develop

## 6) Verifying outputs

After each run, check artifacts:
- android-aab
- android-apk-armv7
- android-apk-arm64
- ios-xcode-project

If store secrets are configured, also verify:
- New release in Google Play Internal Testing
- New build in TestFlight processing queue

## 7) Troubleshooting checklist

### Unity build fails early
- UNITY_LICENSE invalid or expired.
- UNITY_EMAIL/UNITY_PASSWORD incorrect.
- Unity version mismatch with project.

### Google Play upload fails
- GOOGLE_PLAY_SERVICE_ACCOUNT_JSON malformed or wrong permissions.
- ANDROID_PACKAGE_NAME does not match app id.

### iOS signing/archive fails
- Certificate and provisioning profile do not match bundle id/team.
- Wrong APPLE_TEAM_ID.
- Wrong APPLE_SIGNING_CERT_PASSWORD.

### TestFlight upload fails
- Invalid APPSTORE_API_PRIVATE_KEY content.
- Wrong APPSTORE_API_KEY_ID or APPSTORE_ISSUER_ID.

## 8) Security best practices

- Rotate all keys regularly (at least quarterly).
- Use least-privilege roles for Google/Apple service accounts.
- Keep CI credentials separate from personal credentials.
- Never commit private keys, .p12, .p8, or JSON key files.

## 9) Unity Cloud Build alternative

If you prefer Unity-hosted CI:

1. Open Unity Dashboard > DevOps > Build Automation.
2. Connect this repository.
3. Create targets for Android and iOS.
4. Reuse build methods from:
- MuLike.EditorTools.ProjectBuildPipeline in Assets/_Project/Editor/BuildPipeline.cs

This provides an alternative path if self-hosted GitHub runners are not desired.
