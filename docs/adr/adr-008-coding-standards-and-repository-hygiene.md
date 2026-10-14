# ADR-008: Coding Standards and Repository Hygiene

Status: Accepted (review of 2026-09-07, amended at review: trunk-based development)
Date: 2026-09-07
Evidence: the reference codebase's `Directory.Build.props` and `Directory.Packages.props`; the `CLAUDE.md` conventions of the reference codebase and the proof of concept; the rail's documentation policy

## Context

Eight engineers across two organisations build in one repository. The standards below make the codebase read as one author's work and keep the repository free of the decay modes both reference codebases have already paid to learn about (stale documents, unpinned versions, drifting conventions).

## Decision

**Build settings** (root `Directory.Build.props`): `TreatWarningsAsErrors`, `ImplicitUsings` enabled, nullable reference types enabled, embedded debug symbols. **Central package management** (root `Directory.Packages.props`): `ManagePackageVersionsCentrally` with `CentralPackageTransitivePinningEnabled`; versions grouped by framework family property; no version attributes in project files. `global.json` pins the SDK (10.0.x line at repository creation).

**Analysis** (sharpened 2026-09-07 to the rail's decided D13 full gate): `AnalysisLevel` at `latest-recommended` with `EnforceCodeStyleInBuild`; `.editorconfig` carries the style rules; `NuGetAudit` in mode `all` with high-severity advisories failing the build; formatting is enforced (`dotnet format --verify-no-changes` in the pipeline), so style is never review content.

**C# conventions** (carried from the reference codebases): PascalCase public members, `_camelCase` private fields, Allman braces, four-space indentation, file-scoped namespaces, one type per file, `GlobalUsings.cs` per project ordered framework-first. Naming follows the ubiquitous language of the build baseline: aggregate, event and table names use the domain model's words, and shorthand invented in code is a review finding.

**Comments carry the why, never the what** (sharpened 2026-09-07 to the rail's B3 policy): a comment is a non-obvious why on the member it constrains: the rule the code reproduces and its source, an invariant a caller must honour that the type system cannot enforce, or the retirement condition of a deliberate workaround. At most eight lines, present tense; no measurements, incident history, origin stories or restatements of the code. `///` summaries only on shared library surfaces (contracts and building blocks), one to three lines, no `<remarks>`. A number, rule or invariant the code must honour is pinned by a test, not described in prose. Entity names that two sides must agree on (topics, subscriptions, containers) live in one topology type in the contracts library, referenced by both the AppHost and the options defaults (the rail's `RailTopology` pattern).

**Documentation policy** (the rail's, adopted whole): the platform repository carries exactly one high-level `docs/architecture.md` (component map and glossary, no behaviour), the ADRs under `docs/adr/`, active plans, `ONBOARDING.md`, and `CLAUDE.md`. No other standing documents: behaviour lives in code and tests, agreements live in plans that retire when the work lands, and a document that restates code is deleted on sight. Agent working files go to a gitignored scratch folder and are never citations.

**Version control and review: trunk-based development** (review of 2026-09-07). `main` is the trunk, protected and always releasable; work integrates into it at least daily through branches that live hours rather than days, merged by pull request with the pipeline green (all four tiers). There are no long-lived feature, develop or release branches; releases and deploys cut from `main` (per-domain cadence per ADR-002). Work too large for one merge lands incrementally and dark: committed but not yet wired into a host or a reachable path, rather than parked on a branch. (Population control during parallel runs stays scope-by-registry per the NFR register rule 10; dark code is an engineering staging technique, never a population mechanism.) Commits are small and buildable; a PR does one thing (the rail's minimal-diff rule: fix exactly what was asked, report other gaps in prose). Generated artefacts, `bin/`, `obj/`, state files and local settings never land in the repository (`MeterScope`, the empty remnant found in the rail, is the cautionary example).

**Language**: identifiers and code comments in standard technical English; team-facing documents in British English per the team's writing rules.

## Consequences

- A new module inherits every setting from the root files; nothing is configured per project, so drift has nowhere to start.
- Warnings-as-errors plus pinned central versions means dependency and compiler noise is dealt with at introduction time, not accumulated.
- The documentation policy keeps the repository navigable by convention: an agent or engineer looks in exactly five places, and none of them can silently rot because none of them is allowed to describe behaviour.
- The standards are enforced by tooling (pipeline format check, analyzers, architecture tests) rather than reviewer memory, which is what makes them survive team growth.
