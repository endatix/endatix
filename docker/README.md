# Endatix Platform Docker Setup

Welcome to Endatix Platform! This guide will help you quickly set up and run Endatix using Docker containers for **development and testing purposes**. The setup script handles everything automatically - just run it and you're ready to go.

> ⚠️ **Important**: This is a development setup not suitable for production use. It uses HTTP (not HTTPS), default passwords, and simplified configuration. For production deployments, please contact Endatix for guidance on secure, scalable setups.

## What is Endatix?

Endatix is a modern platform for creating and managing SurveyJS based forms and surveys. It consists of:
- **Endatix API**: Backend service for form management and data collection
- **Endatix Hub**: Web interface for creating forms and viewing responses

## Prerequisites

Before proceeding, ensure you have installed:

- **Docker Desktop** (includes Docker and Docker Compose)

Download from: [https://docs.docker.com/get-docker/](https://docs.docker.com/get-docker/)

Tested with Docker version 28.3.2 and Docker Compose v2.38.2.

---

## Quick Start

### Step 1: Get Setup Files

You need these files to get Endatix running:
- `setup.bat` (Windows) or `setup.sh` (Linux/macOS)  
- `docker-compose.yaml`

**If you already have this README**, these files are likely in the same folder. You can skip to Step 2.

**If you need to download them:**

**Option A: Direct Download**
- **Windows**: [setup.bat](https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.bat) + [docker-compose.yaml](https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.yaml)
- **Linux/macOS**: [setup.sh](https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.sh) + [docker-compose.yaml](https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.yaml)

**Option B: Using Command Line**

Windows (PowerShell/Command Prompt):
```bash
curl -o setup.bat https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.bat
curl -o docker-compose.yaml https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.yaml
```

Linux/macOS (Terminal):
```bash
curl -o setup.sh https://raw.githubusercontent.com/endatix/endatix/main/docker/setup.sh
curl -o docker-compose.yaml https://raw.githubusercontent.com/endatix/endatix/main/docker/docker-compose.yaml
```

### Step 2: Check Port Availability

Make sure these ports are free on your machine:
- **8080** - Endatix API  
- **3000** - Endatix Hub
- **18888** - Telemetry dashboard (localhost only)
- **18889** - Telemetry ingest, OTLP/gRPC (localhost only)

### Step 3: Run the Setup Script

**Windows:** Double-click `setup.bat` or run in Command Prompt/PowerShell:
```bash
setup.bat
```

**Linux/macOS:** First make executable, then run:
```bash
chmod +x setup.sh
./setup.sh
```

### Step 4: Enter Admin Credentials

The script will prompt you for:
- **Admin Email**: Default is `admin@endatix.com`
- **Admin Password**: Default is `P@ssw0rd`

> 💡 **Note**: For testing purposes, you can use the defaults. Change these credentials before any production use.

Press Enter to use defaults, or type your own values.

The script will automatically:
1. Download the required Docker images
2. Create and start all containers
3. Set up the database and initial admin user

---

## Access Your Endatix Platform

Once setup is complete, open your browser and visit:

- **📊 Endatix Hub** (main interface): http://localhost:3000
- **🔧 Endatix API** (for developers): http://localhost:8080
- **🔭 Telemetry dashboard** (traces, metrics, logs): http://localhost:18888

Sign in using the admin credentials you set during setup.

> 🔓 **Security Note**: This setup uses HTTP connections for simplicity. Production environments require HTTPS, secure passwords, and additional security measures.

### Telemetry dashboard

Both the API and the Hub export OpenTelemetry data to a bundled [.NET Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone),
so you can see traces, metrics and structured logs from a single UI without running
Loki, Tempo or Grafana. Submit a form and the request shows up as a trace spanning Hub
and API, with the log lines for that request attached to it.

It is bound to **localhost only** and runs **without authentication** — that combination
is deliberate and is why it must stay on `127.0.0.1`. Do not re-publish those ports on
`0.0.0.0`, and do not reuse this service definition in a deployed environment: anyone who
can reach it sees every request and form payload the platform handles.

To point a locally-run API or Hub at it instead of the containerised one, export to
`http://localhost:18889` over gRPC:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:18889 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
OTEL_SERVICE_NAME=endatix-api \
dotnet run
```

---

## Managing Containers

**Stop containers:**
```bash
docker compose -f docker-compose.yaml stop
```

**Start containers again:**
```bash
docker compose -f docker-compose.yaml start
```

**Remove containers (keeps data):**
```bash
docker compose -f docker-compose.yaml down
```

**View logs:**
```bash
docker compose -f docker-compose.yaml logs
```

---

## Need Help?

If you encounter issues:
1. Ensure Docker Desktop is running
2. Check that required ports (8080, 3000) are available
3. Review the setup script output for error messages

For support, visit [endatix.com](https://endatix.com/contact) or [GitHub Discussions](https://github.com/endatix/endatix/discussions).

For **production deployments**, contact Endatix for guidance on secure, scalable setups with HTTPS, proper authentication, monitoring, and backup strategies.

---

**That's it! 🚀 Your Endatix Platform is ready for exploring.**