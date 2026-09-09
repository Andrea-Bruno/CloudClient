# CloudClient — release pipeline & Linux distribution plan (technical reference)

Status: 2026-09-09 (plan phase 1 applied). Scope mirrors the AgentBridge system **minus the
Microsoft Store**: automatic updates with the `IsPrerelease` gate + date versioning,
dependency NuGet packages for CI, platform archives on GitHub Releases, Linux desktop
channels, easy README install.

## Current state (verified 2026-09-09)

- Solution `CloudClient.sln` at repo root; two projects:
  - `Cloud/Cloud.csproj` — the app: **Blazor web + PWA** (`wwwroot/service-worker.js`),
    `Microsoft.NET.Sdk.Web`, `OutputType=WinExe`, **requires administrator/root**
    (exits otherwise), .NET **10**, framework-dependent (expects a system .NET runtime on
    Linux/macOS), ships `install.bat` / `install.sh` / `uninstall.sh` / `INSTALL.txt`.
  - `CloudClient/CloudClient.csproj` — helper library (MonoMac.NetStandard, System.Drawing).
- Dependencies (ProjectReference to sibling repos, not on NuGet): **CloudBox** (from
  `CloudBox/CloudLibraries`), **AppSync** (Andrea-Bruno/AppSync, private), **AntiGithub**
  (Andrea-Bruno/AntiGithub), **UISupportBlazor** (published: `uisupportblazor` 1.25.x),
  **SystemExtra** (published: `systemextra`).
- GitHub: public `Graphene-Lab/CloudClient`, branch `master`, **no workflows** (CI absent),
  no versioning/version gate, README has a basic "Installation" section only.
- NuGet check: `cloudbox`, `appsync`, `antigithub` **absent** from nuget.org →
  a public CI cannot restore the sibling repos → **they must be published first**.

## Architecture to implement (AgentBridge model)

### 1. Versioning + release gate (DONE in phase 1)
Same as AgentBridge: `<IsPrerelease>` (default true) + date version `1.yy.MM.dd`
(`<ReleaseDate>` pin for the gate-off commit so CI UTC never derives the previous day).
A push to `master` with `IsPrerelease=false` triggers `release.yml` → tag `v1.yy.MM.dd`.

### 2. Dependency NuGet publishing (needed before CI can build)
Add to each dependency repo the AgentBridge "core repo" convention: date-versioned pack on
`v*` tag push (`publish.yml`), `NUGET_API_KEY` secret. Package ids to use (check free):
`cloudbox` (from CloudBox/CloudLibraries/CloudBox), `appsync`, `antigithub`
(AntiGithub library). `uisupportblazor` / `systemextra` already published.
Then switch the csprojs to the **dual-reference** pattern
(`ProjectReference Condition="Exists(...)"` + `PackageReference Version="1.*"`) so dev
builds use siblings and CI restores NuGet.

### 3. release.yml (GitHub Release + archives)
Mirror AgentBridge: check-version gate → wait for today's dependency packages →
publish per-RID self-contained? NOTE: Cloud is currently **framework-dependent** (uses
system dotnet + admin). Decide per channel:
- Keep framework-dependent for the classic install (system runtime) **or** switch to
  self-contained single-file archives like AgentBridge (recommended for the stores).
Then create GitHub release with win-x64 / linux-x64(+arm64) / osx assets + changelog.
Auto-update: reuse the existing **AppSync** mechanism wired to the same release tags
(equivalent of AgentBridge AutoUpdate), gated by the same `IsPrerelease`.

### 4. Linux desktop channels (no MS Store)
- **Flatpak** self-published (AgentBridge-Linux pattern) + **Flathub** submission later.
- **Snap Store** (classic confinement request) + **AUR**.
- The app is **admin/root-required** and a **PWA** (installable from its web UI): this
  affects the sandbox/classic story — document clearly per channel (Snap classic is the
  natural fit for a root-level sync/backup tool).

### 5. README "Install & Download"
Add the AgentBridge-style badge block (platforms + latest release), keep the existing
`install.sh`/`install.bat` instructions, add the PWA note and the Linux store links as
they go live.

## Phase checklist
- [x] Phase 1 (repo-local): version gate + date versioning in `Cloud.csproj`; README
      "Install & Download" section; this plan doc.
- [ ] Phase 2: publish `cloudbox`/`appsync`/`antigithub` NuGet packages (per-repo,
      needs NUGET_API_KEY) + dual-reference switch in the csprojs.
- [ ] Phase 3: `release.yml` (archives + tag + release notes) and validation build.
- [ ] Phase 4: AppSync auto-update wiring to releases.
- [ ] Phase 5: Flatpak (self-hosted + Flathub later), Snap classic, AUR; README final.
- [ ] Phase 6: end-to-end green run + verification (WSL where possible).
