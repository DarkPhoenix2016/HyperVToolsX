# Releasing HyperVToolsX

This describes how a HyperVToolsX release is cut. Every push and pull request against `master`/`development` is
built and tested by [`.github/workflows/build.yml`](.github/workflows/build.yml). Pushing a `v*` tag triggers
[`.github/workflows/release.yml`](.github/workflows/release.yml), which builds, tests, publishes and opens a
**draft** GitHub Release with the unsigned artifact attached — signing and publishing the release are still manual,
deliberate steps.

## 1. Update version

Bump the version consistently in `HyperVToolsX.App/HyperVToolsX.App.csproj`:

```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
<InformationalVersion>1.0.1</InformationalVersion>
```

Use [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`, with pre-releases as `-beta.N` / `-rc.N`.

## 2. Update CHANGELOG.md

Add a new `## [x.y.z] - YYYY-MM-DD` section under [Keep a Changelog](https://keepachangelog.com/) conventions,
listing Added / Changed / Fixed.

## 3. Update the README version badge

The `Version` badge near the top of `README.md` should match the new version.

## 4. Test locally on a Hyper-V host

CI only runs the subset of `HyperVToolsX.Tests` that doesn't need a live Hyper-V host (hosted runners don't have
the role). Before tagging, run the **full** suite on a Windows machine with Hyper-V, elevated:

```powershell
dotnet build HyperVToolsX.slnx -c Release
dotnet test HyperVToolsX.Tests -c Release
```

All tests must pass, including the three tagged `Category=RequiresHyperV` (`HyperVProviderTests` and
`TargetValidatorTests.LocalHyperVHost_ShouldPassValidation`).

## 5. Tag

Commit the version/changelog/badge updates to `master`, then:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

Pushing the tag runs `release.yml`: restore, build, the same filtered test run as `build.yml`, `dotnet publish` with
`FolderProfile` (self-contained, single-file, ReadyToRun `win-x64`), a SHA-256 hash of the unsigned exe, and a
**draft** GitHub Release with `HyperVToolsX.exe`, `HyperVToolsX.exe.sha256` and `release-manifest.json` (product,
version, platform, framework, source repo/tag/commit and hash) attached. Check the Actions run
before continuing.

## 6. Verify the built artifact

Download the exe from the draft release (or the workflow's build) and confirm:

- Product name, product version and file version (Properties → Details in Explorer, or
  `(Get-Item .\HyperVToolsX.exe).VersionInfo`) match the version set in step 1
- The application launches, elevates, and can connect to and collect a test host
- `HyperVToolsX.exe /?` shows help and command-line mode still works

## 7. Sign

Authenticode-sign the downloaded executable (the app runs elevated, so an unsigned build triggers SmartScreen/UAC
"unknown publisher" warnings):

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a HyperVToolsX.exe
```

Once code signing via SignPath is set up, this step is replaced by submitting the draft release's artifact for
manual signing approval per the [Code signing policy](README.md#code-signing-policy); the signed artifact SignPath
returns is published as-is and never modified afterward.

## 8. Finalize the draft release

On the draft release opened in step 5:

1. Replace the unsigned `HyperVToolsX.exe` asset with the signed one.
2. Recompute the hash (`Get-FileHash .\HyperVToolsX.exe -Algorithm SHA256`) and replace the SHA-256 in the release
   body, in `HyperVToolsX.exe.sha256`, and in `release-manifest.json`'s `sha256` field — all three must match the
   signed artifact, not the unsigned one the workflow generated.
3. Set `release-manifest.json`'s `signed` field to `true` once it's the signed artifact.
4. Note in the release body that the binary is signed (and by whom, once SignPath is active), or that it is
   unsigned if you're publishing before signing is set up.
5. Publish the release (turn off "draft").

## 9. Verify the download

Download the published GitHub release asset (not the local build) and confirm its hash matches what was recorded,
and that it launches correctly.
