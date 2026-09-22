# Contributing to HyperVToolsX

Thanks for considering a contribution. HyperVToolsX is a small, actively maintained project — issues and pull
requests are welcome.

## Before you start

- For anything non-trivial (a new feature, a behavior change), open an issue first to discuss the approach.
- For bugs, check existing issues before opening a new one, and include enough detail to reproduce
  (HyperVToolsX version, Windows version, steps, expected vs. actual behavior). See [SECURITY.md](SECURITY.md)
  instead if it's a security issue.
- By submitting a contribution, you confirm it is your own work (or appropriately licensed) and that you're
  licensing it under the project's [MIT License](LICENSE).

## Development setup

- [.NET 10 SDK](https://dotnet.microsoft.com/download) with the Windows desktop workload
- Visual Studio 2022/2026 (optional) or the `dotnet` CLI
- Windows 10 1809+ (x64) with the Hyper-V role, for anything that exercises live collection

```powershell
git clone https://github.com/DarkPhoenix2016/HyperVToolsX.git
cd HyperVToolsX
dotnet build HyperVToolsX.slnx -c Release
dotnet test HyperVToolsX.Tests
```

Some provider/validator tests exercise the local machine (`GetLocalHost`, `GetLocalVirtualMachines`, local target
validation) and require a Windows machine with the Hyper-V role, module, and elevation. Tests that need it are
expected to be run on such a machine before submitting.

## Branching and pull requests

- Development happens on the `development` branch; `master` receives merges from it and reflects the current
  release.
- Branch from `development`, keep changes focused (one concern per PR), and follow the existing code style —
  see the layered architecture in the [README](README.md#architecture) (`Core` → `Infrastructure`/`Export` → `App`;
  `Core` and `Infrastructure` must stay provider-agnostic where practical).
- Run `dotnet build` and `dotnet test` before submitting, and mention in the PR description if any tests were
  skipped because they need a Hyper-V host.
- Update `README.md` and `CHANGELOG.md` for user-visible changes.
- Every push and pull request against `master`/`development` is built and tested automatically by
  [`.github/workflows/build.yml`](.github/workflows/build.yml) (the subset of tests that don't need a live
  Hyper-V host — see the README's [testing note](README.md#building-testing-and-publishing)). A green check is
  required before merge; it doesn't replace running the full suite locally if you touched anything
  Hyper-V-related.

## What not to include

Please don't include, in code, tests, commit messages or issues:

- Real customer or employer infrastructure data (hostnames, IPs, credentials, internal scripts)
- Functionality intended to scan for, exploit, or bypass security controls on Hyper-V hosts or other systems —
  HyperVToolsX is an inventory/documentation tool, not a security-testing tool
- Copied code/scripts from other projects without attribution and a compatible license

## Releasing

Releases are cut by the project maintainer; see [RELEASING.md](RELEASING.md) for the process.
