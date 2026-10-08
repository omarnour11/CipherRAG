# CipherRAG

> A security gateway for Retrieval-Augmented Generation (RAG) applications.

CipherRAG protects AI workflows from sensitive-data leakage and unsafe prompts
before they reach an LLM. It combines policy-based access control, PII/PHI
detection, prompt guardrails, document ingestion, audit logging, and
production-oriented observability in a single .NET service.

## Highlights

- Clean Architecture with separate Domain, Application, Infrastructure, and API layers.
- JWT authentication and role-based authorization.
- Attribute-based access policies for department, country, IP/CIDR range, and time window.
- Microsoft Presidio integration for PII/PHI detection and anonymization.
- Local Ollama integration for prompt guardrails and LLM-backed workflows.
- Document ingestion and RAG endpoints with vector embedding services.
- Structured Serilog logging and OpenTelemetry traces/metrics.
- Prometheus, Grafana, and Seq infrastructure through Docker Compose.
- Response compression, rate limiting, global exception handling, EF Core migrations, and seed data.

## Architecture

```text
CipherRAG.API             Minimal APIs, auth, middleware, Swagger
    |
CipherRAG.Application     DTOs, contracts, use-case abstractions
    |
CipherRAG.Infrastructure  EF Core, SQL Server, RAG, Presidio, Ollama
    |
CipherRAG.Domain          Entities, value objects, policy rules
```

## Requirements

- .NET SDK 10.0
- SQL Server (local or remote)
- Docker Desktop
- Ollama with `llama3` and `llama-guard3:1b` for the local AI workflow
- PowerShell on Windows, or an equivalent shell

## Quick start

### 1. Clone the repository

```powershell
git clone <repository-url>
cd CipherRAG
```

### 2. Configure SQL Server and secrets

The API expects a connection string named `DefaultConnection`. For a local
Windows SQL Server instance, use .NET user secrets and generate a random JWT
signing key:

```powershell
dotnet user-secrets init --project .\CipherRAG.API\CipherRAG.API.csproj

$bytes = New-Object byte[] 64
$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$jwtKey = [Convert]::ToBase64String($bytes)

dotnet user-secrets set "Jwt:Key" $jwtKey --project .\CipherRAG.API\CipherRAG.API.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Server=localhost;Database=CipherRAGDb;Trusted_Connection=True;TrustServerCertificate=True;" `
  --project .\CipherRAG.API\CipherRAG.API.csproj
```

For SQL authentication or a remote server, replace the connection string with
the value supplied by your administrator. Never commit connection strings,
JWT keys, passwords, or user-secrets files.

### 3. Start local infrastructure

Compose automatically reads the project-level `.env` file. The repository
includes a local `.env` for development; change its value once if you want a
different Grafana or Seq password. The file is ignored by Git and must not be
committed. To set this up in a fresh clone instead:

```powershell
Copy-Item .\.env.example .\.env
# Edit .\.env and set both passwords once.
docker compose up -d
```

You do not need to set the password again for later `docker compose` commands.
Grafana and Seq also keep their initialized state in the `grafana-data` and
`seq-data` volumes.

Run Compose from the repository root (`C:\Users\hp\Downloads\CipherRAG`), where
the `.env` file is located. If your terminal is currently in
`CipherRAG.API`, use:

```powershell
docker compose -f ..\docker-compose.yml --env-file ..\.env up -d
```

`.\run.ps1` automatically switches to the repository root before starting
Compose, so it can be launched from any directory.

The Compose file uses the image tags declared in `docker-compose.yml`. If the
images are already cached locally and Docker must not pull anything:

```powershell
docker compose up -d --pull never
```

Seq authentication is enabled by default. Set
`SEQ_FIRSTRUN_NOAUTHENTICATION=true` only for an isolated local environment.

### 4. Prepare Ollama (optional for database/API-only work)

```powershell
ollama pull llama3
ollama pull llama-guard3:1b
```

### 5. Start the API

Run from the repository root:

```powershell
dotnet run --project .\CipherRAG.API\CipherRAG.API.csproj --launch-profile http
```

The Development profile uses `http://localhost:5221`. To use the port
configured in `prometheus.yml`, run on port 5000:

```powershell
dotnet run --project .\CipherRAG.API\CipherRAG.API.csproj --urls http://localhost:5000
```

The launcher is also available:

```powershell
.\run.ps1
```

The web UI is available at the URL printed by ASP.NET Core. Swagger is
available at `/swagger` in Development.

### 6. Verify the installation

```powershell
docker compose ps
dotnet test .\CipherRAG.sln
```

If startup reports `The ConnectionString property has not been initialized`,
`DefaultConnection` is missing or empty. If startup reports an empty JWT key,
configure `Jwt:Key` with a random value; do not use an example placeholder in
production.

If a .NET build reports that `CipherRAG.API.dll` or a project DLL is locked by
`CipherRAG.API.exe`, stop the existing API process with `Ctrl+C` in the terminal
where it is running, then start the API again. If the process was started with
Administrator privileges, run PowerShell as Administrator before using
`.\run.ps1`.

If chat requests take a long time while Ollama or Presidio is stopped, the API
falls back to its local security and response logic after a short timeout.
Start Ollama and make sure the Presidio Analyzer container is healthy for the
full local AI workflow.

Common PII patterns are masked locally before Presidio is called. This avoids a
network round-trip for requests containing recognized phone numbers, emails,
names, or locations; Presidio is used for prompts that need broader NER.

## Local services

| Service | URL |
|---|---|
| CipherRAG API/UI | http://localhost:5221 or http://localhost:5000 |
| Swagger (Development) | http://localhost:5221/swagger |
| Presidio Analyzer | http://localhost:5001 |
| Presidio Anonymizer | http://localhost:5002 |
| Seq | http://localhost:8081 |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3001 |
| Ollama | http://localhost:11434 |

The default Compose stack is for local development only. Change default
passwords, disable unauthenticated Seq, and protect monitoring endpoints
before any shared or production deployment.

## Testing

Run all tests:

```powershell
dotnet test .\CipherRAG.sln
```

Run the domain/service tests with coverage:

```powershell
dotnet test .\tests\CipherRAG.Domain.Tests\CipherRAG.Domain.Tests.csproj --collect:"XPlat Code Coverage"
```

## Configuration

Do not commit secrets. Configure these values through user secrets, environment
variables, or a deployment secret manager:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `LLM__OllamaEndpoint`
- `Guardrail__ApiUrl`
- `Presidio__ApiUrl`

Configuration keys use double underscores in environment variables:

```powershell
$env:ConnectionStrings__DefaultConnection = "your-connection-string"
$env:Jwt__Key = "your-random-key"
```

## Production checklist

- Use managed SQL Server with encrypted connections and least-privilege credentials.
- Supply a random JWT signing key through a secret manager.
- Restrict CORS to the deployed frontend origin.
- Run behind HTTPS and a reverse proxy/WAF.
- Secure and monitor Ollama and Presidio dependencies.
- Enable authentication for Seq, Prometheus, and Grafana; rotate all credentials.
- Disable the Developer Exception Page and Swagger unless explicitly protected.
- Add backups, alerting, log retention, and vulnerability scanning.
- Run migrations as a controlled deployment step in regulated environments.
- Keep `Database:ApplyMigrationsOnStartup` and `Database:SeedOnStartup` disabled outside local development.

## Scope and disclaimer

CipherRAG is a portfolio/reference implementation and is not a certification
of HIPAA, GDPR, or any other regulatory compliance. A production deployment
requires a formal threat model, penetration testing, operational controls, and
organization-specific compliance review.

## License

No license has been selected yet. Add a license before accepting external
contributions or redistributing the project.

## Collaboration and attribution

Contributions are welcome once a project license and contribution policy have
been selected. When collaborating, describe each contributor's substantive work
in the pull request or commit description and credit collaborators in release
notes when appropriate. If you use code, documentation, design, or
AI-assisted output from another person, project, or tool, check its license or
terms and mention the source and nature of the contribution. Do not add anyone
as a contributor or co-author without their consent.
