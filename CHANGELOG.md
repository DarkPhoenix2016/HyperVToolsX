# Changelog

All notable changes to HyperVToolsX are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/) and versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-09-20

First production release.

### Added
- **vSummary tab** columns: vCPUs, Memory Startup (follows the size-unit preference), Memory Usage as a percentage of
  startup memory, Checkpoints, Notes, NIC Name, Switch Name, IP Addresses, MAC Address, VLAN Mode, VLAN List and
  Storage Paths. One row per VM; several adapters, IPs or disk paths share one cell separated by `; `.
- Rolling per-user file log in `%LOCALAPPDATA%\HyperVToolsX\logs` (14 days, 10 MB per day, passwords redacted).
- Crash handlers: unexpected errors are logged and reported instead of silently closing the app.
- Command-line switches `/optimeout:<secs>`, `/trusthosts`, `/passwordfile:<file>`; `HVTX_PASSWORD` environment variable.
- "Allow TrustedHosts change" option in Connection Settings.
- Per-section isolation in the collection script: one failing cmdlet now skips only that section and is logged as a
  warning instead of failing the whole host.

### Changed
- Scripts and passwords are delivered to Windows PowerShell over stdin, never through a temp file, the environment or
  the command line.
- Every remote run has a hard deadline (default 300 s) and its process is killed when exceeded.
- At most 8 PowerShell processes run at once across all hosts and cluster nodes.
- Validation is skipped at collection time when the target was validated in the last 5 minutes.
- A failed ping no longer blocks a target; the WinRM check decides.
- The TrustedHosts list is only modified after explicit opt-in; Basic authentication over HTTP is refused.
- The password is cleared when the Target Manager closes.
- Release build uses ReadyToRun; a single publish profile replaces the two earlier ones.
- Removed the "BETA" badges from the main window and About dialog.

### Fixed
- Windows PowerShell 5.1 child processes no longer inherit the PowerShell 7 `PSModulePath` of the app, which could stop
  `ConvertTo-SecureString` and module autoloading from working.

## [0.1.0-beta.2]

- Cluster owner node no longer counted again as a separate host.
- Excel and CSV export module; custom tab templates.
- UI revamp.

## [0.1.0-beta.1]

- First public beta: local and WinRM collection, staged target validation, cluster discovery, fourteen inventory
  tabs, search and filter, live log, Excel export, command-line mode.
