# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| Latest stable release (currently `1.0.0`) | Yes |
| Older stable releases | Best effort |
| Pre-release / beta tags | No |

## Reporting a vulnerability

Please do not open a public GitHub issue for security-sensitive reports.

Preferred: use GitHub's private vulnerability reporting for this repository (**Security** tab →
**Report a vulnerability**) at
[github.com/DarkPhoenix2016/HyperVToolsX/security](https://github.com/DarkPhoenix2016/HyperVToolsX/security).

If that is not available, open a normal issue asking to be contacted privately, without including any
security-sensitive details in the issue body.

Please include, where relevant:

- HyperVToolsX version and how it was built/obtained
- Windows version
- A description of the issue, steps to reproduce, expected vs. actual behavior
- Relevant log excerpts from `%LOCALAPPDATA%\HyperVToolsX\logs` with sensitive information removed

Please never include:

- Passwords, access tokens or private keys
- Real customer or infrastructure credentials
- Sensitive infrastructure details (internal hostnames, IPs) beyond what's needed to reproduce the issue

## Response

This is a single-maintainer open-source project. There is no guaranteed SLA, but security reports are prioritized
over other issues and will be acknowledged as soon as reasonably possible.

## Scope

In scope: the HyperVToolsX application code in this repository (`HyperVToolsX.App`, `HyperVToolsX.Core`,
`HyperVToolsX.Infrastructure`, `HyperVToolsX.Export`).

Out of scope: vulnerabilities in third-party dependencies (see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)) —
please report those upstream — and vulnerabilities in Windows, Hyper-V, WinRM or PowerShell itself, which should be
reported to Microsoft.

See also [PRIVACY.md](PRIVACY.md) for how HyperVToolsX handles data, and the
[Security notes](README.md#security-notes) section of the README for the application's current security design.
