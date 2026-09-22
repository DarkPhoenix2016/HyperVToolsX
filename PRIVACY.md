# Privacy Policy

Last updated: 2026-09-22

## Overview

HyperVToolsX is an open-source Windows desktop application for collecting and displaying Hyper-V infrastructure
inventory information. This document describes what data the application handles and where it goes.

## No telemetry

HyperVToolsX does not include advertising, analytics, telemetry, crash reporting, update checks or user profiling.
It does not intentionally send application usage statistics to the developer or to any HyperVToolsX-operated
service, because none exists — there is no backend. The application only ever talks to the Hyper-V targets you give
it.

## Hyper-V communication

HyperVToolsX communicates only with the hosts, clusters and IP addresses you explicitly add through
`File → Connect...` or the `/host` / `/hostfile` command-line options. It does not scan networks, discover targets
on its own, or contact any endpoint you did not configure.

Collection of a target may retrieve:

- Host, cluster and VM names, IDs and states
- IP and MAC addresses, network adapter and VLAN configuration
- CPU, memory, storage and VHD configuration and usage
- Checkpoint, integration-services and replication state
- Operating system and Hyper-V version information

This data stays local: it is cached in memory while the application runs, optionally exported to `.xlsx`/`.csv`
files you choose the destination for, and optionally written to the local application log. HyperVToolsX does not
upload it anywhere.

## Credentials

Credentials entered on the Connection Settings tab, or passed via `/user`, `/password`, `/passwordfile` or the
`HVTX_PASSWORD` environment variable, are used only to authenticate the configured WinRM connection for that run.

- Explicit passwords are delivered to the child Windows PowerShell process over its stdin pipe as a `SecureString`,
  never via a temp file, the process environment or the command line.
- Passwords are cleared from memory when the Target Manager closes.
- The file log redacts `password=`-style text.
- Passwords are never transmitted to the HyperVToolsX developer or any third party.

See [Security notes](README.md#security-notes) in the README for the full technical detail.

## Local logs

HyperVToolsX writes a rolling per-user log to `%LOCALAPPDATA%\HyperVToolsX\logs` (14 days retained, 10 MB per day)
for troubleshooting. Logs stay on your machine and are never transmitted anywhere by the application. Because logs
can contain infrastructure details (host names, IPs, VM names), review them before sharing outside your
organization — for example when attaching one to a GitHub issue.

## GitHub

Source code and releases are hosted on GitHub. Downloading the application, filing issues or opening pull requests
is subject to [GitHub's own privacy policy](https://docs.github.com/en/site-policy/privacy-policies/github-privacy-statement).

## Third-party components

HyperVToolsX bundles the open-source components listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). None of
them are configured by this project to phone home; their own licenses and any privacy terms apply to their code.

## Changes to this policy

If HyperVToolsX's data handling changes (for example, if telemetry were ever added), this document and the
[Code signing policy](README.md#code-signing-policy) section of the README will be updated to describe it, and it
would be opt-in.

## Contact

Questions or concerns: open an issue at
[github.com/DarkPhoenix2016/HyperVToolsX/issues](https://github.com/DarkPhoenix2016/HyperVToolsX/issues).
