# Third-Party Notices

HyperVToolsX is written and maintained by Charitha Piyumal and is released under the [MIT License](LICENSE). It
builds on the following open-source NuGet packages, each under its own license. This list reflects the versions
currently referenced by the `.csproj` files in this repository; verify against the `.csproj` files if it's been a
while since this was updated.

| Component | Version | Used by | License | Purpose |
| --- | --- | --- | --- | --- |
| [Microsoft.PowerShell.SDK](https://www.nuget.org/packages/Microsoft.PowerShell.SDK) | 7.6.6 | `HyperVToolsX.Infrastructure` | MIT | Hosts PowerShell to run the Hyper-V inventory scripts, locally and over WinRM |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) | 0.105.1 | `HyperVToolsX.Export`, `HyperVToolsX.Tests` | MIT | Writes the `.xlsx` inventory export |
| [xunit](https://github.com/xunit/xunit) | 2.9.3 | `HyperVToolsX.Tests` | Apache-2.0 | Test framework (test-only, not shipped in releases) |
| [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit) | 3.1.4 | `HyperVToolsX.Tests` | Apache-2.0 | Test discovery/runner (test-only, not shipped in releases) |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | 18.3.0 | `HyperVToolsX.Tests` | MIT | .NET test SDK (test-only, not shipped in releases) |

## Runtime

Self-contained published builds also embed the .NET runtime itself, licensed under the
[MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) by the .NET Foundation and contributors.

## Scope

Only `Microsoft.PowerShell.SDK` and `ClosedXML` (plus the .NET runtime) ship inside the published `HyperVToolsX.exe`.
`xunit`, `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` are used to build and run the test suite
(`HyperVToolsX.Tests`) and are not included in release binaries.

## PowerShell scripts

The Windows PowerShell inventory/collection scripts embedded in `HyperVToolsX.Infrastructure` and
`HyperVToolsX.App` (invoked via `RemoteScriptRunner`/`PowerShellExecutor`) are original work written for this
project by Charitha Piyumal and are covered by the project's own [MIT License](LICENSE), not by a third party. They
call into the standard Hyper-V PowerShell module (`Get-VM`, `Get-VMHost`, `Get-ClusterNode`, etc.), which ships
with Windows/Hyper-V and is not redistributed by this project.

## Code signing

Only `HyperVToolsX.exe` — built from this repository's own source — is submitted for signing under the project's
SignPath subscription. Third-party components listed above are not resigned by this project; their own upstream
signing, if any, is unaffected. See the [Code signing policy](README.md#code-signing-policy) in the README.

## Corrections

If a license or attribution above is inaccurate or a dependency changed, please open an issue or pull request.
