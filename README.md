# EchoPhase

## Introduction
EchoPhase is a platform for managing bots and integrating with various API services.
It allows you to easily automate tasks, connect different services, and manage the bot lifecycle.

## Commands

This project uses [just](https://github.com/casey/just) as a command runner for Docker operations.

### Prerequisites

Install just:
```bash
# macOS
brew install just

# Linux
curl --proto '=https' --tlsv1.2 -sSf https://just.systems/install.sh | bash -s -- --to /usr/local/bin

# Windows
scoop install just
```

### Quick Start

**Show all available commands:**
```bash
just
```

### Frontend

**Build Docker image:**
```bash
just docker-build-frontend
```

**Tag Docker image:**
```bash
just docker-tag-frontend          # uses default version
just docker-tag-frontend 1.2.3    # uses specific version
```

**Push Docker image:**
```bash
just docker-push-frontend         # uses default version
just docker-push-frontend 1.2.3   # uses specific version
```

**All Docker commands at once:**
```bash
just docker-publish-frontend         # uses default version
just docker-publish-frontend 2.0.0   # uses specific version
```

#### Configuration

**Environment variables (`.env`):**
- `FRONTEND_IMAGE` - `echophase-frontend`
- `DOCKER_REGISTRY` - `localhost:5000`
- `VERSION` - `1.0.0` (default version)

Build context: `./frontend/`

### Backend

**Build Docker image:**
```bash
just docker-build-backend
```

**Tag and push:**
```bash
just docker-publish-backend         # uses default version
just docker-publish-backend 1.5.0   # uses specific version
```

#### Configuration

**Environment variables (`.env`):**
- `BACKEND_IMAGE` - `echophase`
- `DOCKER_REGISTRY` - `localhost:5000`
- `VERSION` - `1.0.0` (default version)

Build context: `./src/`

### Build Everything

**Build, tag and push all images:**
```bash
just docker-publish-all            # uses default version
just docker-publish-all 1.0.0      # uses specific version
```

This creates the following tags:
- `localhost:5000/echophase-frontend:1.0.0`
- `localhost:5000/echophase-frontend:latest`
- `localhost:5000/echophase-backend:1.0.0`
- `localhost:5000/echophase-backend:latest`

### Utility Commands

**Show Docker configuration:**
```bash
just docker-info
```

**Clean local images:**
```bash
just docker-clean
```

**Clean build artifacts:**
```bash
just clean
```

### Publishing Images

Before continuing, make sure you have built the images and pushed them to any registry of your choice, since at the moment the images are not publicly available.
It's best to do this locally _(default build)_.

**Launching local registry:**
```bash
docker run -d -p 5000:5000 --name registry registry:2
```

**Publishing to local registry:**
```bash
just docker-publish-all 1.0.0
```

### Launching Cluster

**Launching default Helm:**
```bash
helm install echophase helm --namespace echophase --create-namespace
```

**Launching default Docker Compose:**
```bash
cd compose && docker-compose up -d
```

## Advanced Usage

### Custom Registry

```bash
DOCKER_REGISTRY=myregistry.com just docker-publish-all 2.0.0
```

### Custom Image Names

```bash
FRONTEND_IMAGE=my-frontend BACKEND_IMAGE=my-backend just docker-publish-all 1.5.0
```

### Environment Variables Override

```bash
DOCKER_REGISTRY=myregistry.com \
FRONTEND_IMAGE=custom-frontend \
BACKEND_IMAGE=custom-backend \
VERSION=3.0.0 \
just docker-publish-all
```

## Project Structure

```
.
├── frontend/         # Frontend application
├── src/              # Backend application
├── helm/             # Kubernetes Helm charts
├── compose/          # Docker Compose configuration
├── justfile          # Main task runner
├── just              # Specific tasks
├── .env              # Environment configuration
```

## Docker Commands Reference

| Command | Description |
|---------|-------------|
| `just docker-build-frontend` | Build frontend image |
| `just docker-build-backend` | Build backend image |
| `just docker-build-all` | Build all images |
| `just docker-tag-frontend [version]` | Tag frontend image |
| `just docker-tag-backend [version]` | Tag backend image |
| `just docker-push-frontend [version]` | Push frontend to registry |
| `just docker-push-backend [version]` | Push backend to registry |
| `just docker-publish-frontend [version]` | Build, tag, and push frontend |
| `just docker-publish-backend [version]` | Build, tag, and push backend |
| `just docker-publish-all [version]` | Build, tag, and push everything |
| `just docker-info` | Show Docker configuration |
| `just docker-clean` | Remove local images |
