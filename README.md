# Acme Platform

The V2 platform for the Acme energy business. It receives, records and interprets the data the
business runs on: meter readings and market messages from network operators today, consumption and
billing ledgers next. It is one .NET solution split into six business domains (Growth, Engage,
Metering & Consumption, Billing & Pay, Customer Experience, Platform & Data Insights). Each domain
deploys its own services to Azure Container Apps and talks to the other domains through events on
Azure Service Bus.

| Document | What it gives you |
| --- | --- |
| [docs/architecture.md](docs/architecture.md) | The map of what the code does and which piece to open |
| [docs/repository-structure.md](docs/repository-structure.md) | Where everything lives and why |
| [docs/adr/](docs/adr/) | The architecture decisions |

## Prerequisites

| Tool | Version | Needed for |
| --- | --- | --- |
| Git | Any recent release | Cloning the repository |
| .NET SDK | 10.0.301, or a later 10.0.3xx patch (pinned in `global.json`) | Building, testing and running |
| Aspire CLI | 13.4 or later | `aspire run`, which starts the local stack |
| Docker Desktop | Current release | The local SQL Server, Service Bus emulator and Azurite storage emulator. Also the integration tests. |

Azure CLI and Terragrunt are only needed to deploy infrastructure. They are not needed to run the
platform locally.

Two hardware notes:

- **Apple Silicon Mac.** SQL Server and the Service Bus emulator are published as x86_64 container
  images only. Docker Desktop runs them through Rosetta. In Docker Desktop open Settings, General,
  and enable "Use Rosetta for x86_64/amd64 emulation on Apple Silicon". Install Rosetta first if
  macOS asks for it (`softwareupdate --install-rosetta`).
- **Windows.** Docker Desktop needs the WSL 2 backend and a Windows release Docker Desktop still supports.

The first `aspire run` downloads the three container images. Allow several minutes and a few
gigabytes of disk.

## Run it locally on macOS

1. Install the .NET 10 SDK. Download the installer from
   <https://dotnet.microsoft.com/download/dotnet/10.0>, or run `brew install --cask dotnet-sdk`.
   Check the version:

   ```bash
   dotnet --version    # 10.0.301 or a later 10.0.3xx
   ```

2. Install Docker Desktop from <https://www.docker.com/products/docker-desktop/> and start it. On
   Apple Silicon, enable the Rosetta setting described above.

3. Install the Aspire CLI. Either command works. Open a new terminal afterwards so the CLI is on
   your path.

   ```bash
   curl -sSL https://aspire.dev/install.sh | bash
   # or
   brew install --cask microsoft/aspire/aspire
   ```

4. Trust the local HTTPS development certificate. The Aspire dashboard and the hosts use it.

   ```bash
   aspire certs trust
   ```

5. Clone the repository and check the environment. `aspire doctor` reports the CLI, SDK and
   container runtime it found.

   ```bash
   git clone <repository-url> acme-platform
   cd acme-platform
   aspire doctor
   ```

6. Build everything. The build treats warnings as errors, so a clean build is the expected result.

   ```bash
   dotnet build AcmePlatform.slnx
   ```

7. Start the Metering & Consumption domain. This starts SQL Server, the Service Bus emulator,
   Azurite, the two hosts and the local blob-event poller.

   ```bash
   aspire run --project domains/metering-consumption/src/AppHost
   ```

   The CLI prints the dashboard address and opens it in the browser. Wait until every resource
   shows as Running and the two hosts show as Healthy. In Development the hosts apply any schema
   scripts the database has not seen yet on start.

8. Stop with `Ctrl+C`. The containers are removed with the run. Nothing persists between runs.

To run every domain together instead of one, use the platform AppHost:

```bash
aspire run --project platform/src/AppHost
```

## Run it locally on Windows

All commands run in PowerShell.

1. Install the .NET 10 SDK. Download the installer from
   <https://dotnet.microsoft.com/download/dotnet/10.0>, or run
   `winget install Microsoft.DotNet.SDK.10`. Check the version:

   ```powershell
   dotnet --version    # 10.0.301 or a later 10.0.3xx
   ```

2. Enable WSL 2 if it is not already enabled, then restart when asked:

   ```powershell
   wsl --install
   ```

3. Install Docker Desktop from <https://www.docker.com/products/docker-desktop/> (or
   `winget install Docker.DockerDesktop`) and start it. In Settings, General, confirm "Use the
   WSL 2 based engine" is enabled.

4. Install the Aspire CLI. Open a new PowerShell window afterwards so the CLI is on your path.

   ```powershell
   irm https://aspire.dev/install.ps1 | iex
   ```

5. Trust the local HTTPS development certificate. Accept the Windows security prompt.

   ```powershell
   aspire certs trust
   ```

6. Clone the repository and check the environment:

   ```powershell
   git clone <repository-url> acme-platform
   cd acme-platform
   aspire doctor
   ```

7. Build everything:

   ```powershell
   dotnet build AcmePlatform.slnx
   ```

8. Start the Metering & Consumption domain:

   ```powershell
   aspire run --project domains\metering-consumption\src\AppHost
   ```

   The CLI prints the dashboard address and opens it in the browser. Wait until every resource
   shows as Running and the two hosts show as Healthy.

9. Stop with `Ctrl+C`.

The platform AppHost, which runs every domain together, is
`aspire run --project platform\src\AppHost`.

## Check that it works

The quickest check is the acceptance suite, run against the local stack while it is up. It puts ESB
files in the drop container and publishes the events of the legacy V1 platform's reader (not in this repository), then asserts the receipts, the
archive, the ledger, the supply point history and the profile rows. Every developer and agent runs it
before a push.

```bash
aspire start --apphost domains/metering-consumption/src/AppHost --non-interactive
aspire wait metering-consumption-host --apphost domains/metering-consumption/src/AppHost
dotnet test domains/metering-consumption/tests/MeteringConsumption.AcceptanceTests
aspire stop --apphost domains/metering-consumption/src/AppHost
```

To watch the flow yourself while `aspire run` is up:

1. Connect Azure Storage Explorer to the local emulator on the blob port in the table below.
2. Upload the sample file to the `esb-drop` container with Azure Storage Explorer or the
   Azure CLI. The sample is
   `domains/metering-consumption/tests/MeteringConsumption.TestsShared/samples/DSO_MIM_343_1782352213415_3805737.extract.xml`.
3. Within a few seconds the poller announces the blob, the receiver host seals it in the archive, and the
   processing host records the outcome. Follow it in the dashboard's Console logs and Traces.

The local stack listens on the same ports every run, none of them a standard port:

| Resource | Address |
| --- | --- |
| SQL Server | `localhost,14330`, login `sa`, password `LocalDev!Passw0rd`, database `metering-consumption` |
| Azurite (blob, queue, table) | `127.0.0.1:10010`, `10011`, `10012`, account `devstoreaccount1` |
| Service Bus emulator | `localhost:5673` |
| Processing host | `http://localhost:5320` |
| Receiver host | `http://localhost:5330` |

With the stack up, the acceptance suite drives it end to end. It starts nothing itself; if it cannot
reach a store or a host, start the AppHost and run it again. Set `ACCEPTANCE_ENVIRONMENT` to `dev`,
`tst` or `prd` (with `ACCEPTANCE_METR_MARKETMSG_URL` and `ACCEPTANCE_METR_RECEIVER_URL`) to drive a
deployed environment instead.

```bash
dotnet test domains/metering-consumption/tests/MeteringConsumption.AcceptanceTests
```

## Everyday commands

Run these from the repository root. Docker must be running for anything beyond the unit and
architecture tests.

| Task | Command |
| --- | --- |
| Build everything with zero warnings | `dotnet build AcmePlatform.slnx` |
| Build one domain | `dotnet build domains/metering-consumption/MeteringConsumption.slnx` |
| Run a domain's unit and integration tests | `dotnet test domains/metering-consumption/MeteringConsumption.slnx --filter "Category!=AcceptanceTests"` |
| Run the acceptance suite (before every push) | `dotnet test domains/metering-consumption/tests/MeteringConsumption.AcceptanceTests` against the AppHost now running |
| Run only the unit tests (no Docker) | `dotnet test domains/metering-consumption/MeteringConsumption.slnx --filter Category=UnitTests` |
| Run the architecture isolation rules (fast, no Docker) | `dotnet test platform/src/architecture-tests/ArchitectureTests` |
| Add a database migration | Add the next numbered SQL script to `domains/metering-consumption/src/Modules/MarketMessaging/Infrastructure/Schema/Scripts/`, for example `Metr-MarketMessaging-00004 - <What it changes>.sql`: the domain, the module, then the next number. It is embedded on build and DbUp applies it once per database, tracked in the `SchemaVersions` table. A script that has run never changes. |

On Windows, PowerShell accepts the forward slashes in these paths as written.
