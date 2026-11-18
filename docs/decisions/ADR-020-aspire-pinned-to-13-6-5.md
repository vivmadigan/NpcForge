# ADR-020: Aspire is pinned to 13.6.5, not the 13.4.2 the spike ran

- **Status:** accepted
- **Date:** 2026-11-18
- **Supersedes:** no record whole. It records the version that the last sentence of ADR-019
  decision 9 pins ("The thin-slice PR pins the Aspire version it was checked on"), which is not the
  13.4.2 that ADR-019's spike ran.

## Context

ADR-019's spike ran Aspire 13.4.2, and decision 9 has the thin-slice PR pin the version it was
checked on. The spike listed 13.6.5, the current version, as not checked.

The thin slice restored the AppHost on 13.4.2 on 2026-11-18. NuGet's audit reported:

- `Aspire.Dashboard.Sdk.win-x64` 13.4.2: critical (GHSA-4g86-54fm-h9h8).
- `Aspire.Hosting` and `Aspire.Hosting.AppHost` 13.4.2: high (GHSA-f7vh-h5g3-7jrh).
- `MessagePack` 2.5.192, which those bring: two high and ten moderate advisories.

On 13.6.5, `dotnet list NpcForge.AppHost package --vulnerable --include-transitive` reports none.

## Options considered

- **13.4.2, the spike's version.** The spike checked it. Every build prints the audit's warnings,
  and the dashboard the owner opens carries a critical advisory.
- **13.4.2 with the audit warnings suppressed.** The build is quiet, and every later advisory is
  hidden as well.
- **13.6.5.** No advisory. The spike did not run it, so the thin slice checks what the spike checked.

## Decision

Aspire is pinned to 13.6.5, in both places `NpcForge.AppHost.csproj` names it: the AppHost SDK and
`Aspire.Hosting.JavaScript`. The NuGet audit stays on. `AspireUseCliBundle` is set to false in the
project, and ASPIRE010, which says the AppHost does not use the Aspire CLI bundle, is suppressed: the
owner starts the AppHost with `dotnet run`, and DCP and the dashboard come from NuGet.

## Why

The thin slice checked 13.6.5 on what the spike checked for 13.4.2. Vite started from the unchanged
scripts on a port Aspire picked, `WithReference` gave it `API_HTTP`, `/api/saves` through Vite
returned the saves, and stopping the AppHost stopped Vite, the API and DCP. The pin exists so that the
version is a checked one. 13.6.5 is checked and carries no advisory.

## Consequences

- An Aspire upgrade changes both version strings in `NpcForge.AppHost.csproj` together.
- A first restore puts 336 MB of Aspire packages in the NuGet cache (measured 2026-11-18).
- Every build checks the Aspire packages against the audit, so a new advisory shows as a warning.
- Aspire's docs for ASPIRE010 say the CLI bundle will become the only supported source of DCP and the
  dashboard. A later upgrade may then need the Aspire CLI, and a record of its own.
- Still not checked, as in ADR-019: that the dashboard shows the API's stderr as its log.
