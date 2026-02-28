# docker.just - Docker-specific tasks

# Docker configuration from environment or defaults
DOCKER_REGISTRY := env_var_or_default('DOCKER_REGISTRY', 'localhost:5000')
FRONTEND_IMAGE := env_var_or_default('FRONTEND_IMAGE', 'echophase/ui')
BACKEND_IMAGE := env_var_or_default('BACKEND_IMAGE', 'echophase/server')
DEFAULT_VERSION := env_var_or_default('VERSION', '1.0.0')

# ========================================
# FRONTEND TASKS
# ========================================

# Build frontend Docker image
docker-build-frontend:
    @echo "Building frontend Docker image: {{FRONTEND_IMAGE}}..."
    docker build -t {{FRONTEND_IMAGE}} ./frontend
    @echo "Frontend build complete!"

# Tag frontend image (version can be passed as argument)
docker-tag-frontend version=DEFAULT_VERSION:
    @echo "Tagging frontend image with version: {{version}}..."
    docker tag {{FRONTEND_IMAGE}} {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:{{version}}
    docker tag {{FRONTEND_IMAGE}} {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:latest
    @echo "Tagged as {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:{{version}}"
    @echo "Tagged as {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:latest"

# Push frontend image to registry
docker-push-frontend version=DEFAULT_VERSION: (docker-tag-frontend version)
    @echo "Pushing frontend image to registry..."
    docker push {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:{{version}}
    docker push {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:latest
    @echo "Frontend push complete!"

# Build, tag and push frontend (full workflow)
docker-publish-frontend version=DEFAULT_VERSION: docker-build-frontend (docker-push-frontend version)
    @echo "Frontend publish complete!"
    @echo "Image: {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:{{version}}"
    @echo "Image: {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:latest"

# ========================================
# BACKEND TASKS
# ========================================

# Build backend Docker image
docker-build-backend:
    @echo "Building backend Docker image: {{BACKEND_IMAGE}}..."
    docker build -t {{BACKEND_IMAGE}} ./src
    @echo "Backend build complete!"

# Tag backend image (version can be passed as argument)
docker-tag-backend version=DEFAULT_VERSION:
    @echo "Tagging backend image with version: {{version}}..."
    docker tag {{BACKEND_IMAGE}} {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:{{version}}
    docker tag {{BACKEND_IMAGE}} {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:latest
    @echo "Tagged as {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:{{version}}"
    @echo "Tagged as {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:latest"

# Push backend image to registry
docker-push-backend version=DEFAULT_VERSION: (docker-tag-backend version)
    @echo "Pushing backend image to registry..."
    docker push {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:{{version}}
    docker push {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:latest
    @echo "Backend push complete!"

# Build, tag and push backend (full workflow)
docker-publish-backend version=DEFAULT_VERSION: docker-build-backend (docker-push-backend version)
    @echo "Backend publish complete!"
    @echo "Image: {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:{{version}}"
    @echo "Image: {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:latest"

# ========================================
# COMBINED TASKS
# ========================================

# Build both frontend and backend
docker-build-all: docker-build-frontend docker-build-backend
    @echo "All images built!"

# Tag both frontend and backend
docker-tag-all version=DEFAULT_VERSION: (docker-tag-frontend version) (docker-tag-backend version)
    @echo "All images tagged!"

# Push both frontend and backend
docker-push-all version=DEFAULT_VERSION: (docker-push-frontend version) (docker-push-backend version)
    @echo "All images pushed!"

# Publish both frontend and backend (full workflow)
docker-publish-all version=DEFAULT_VERSION: (docker-publish-frontend version) (docker-publish-backend version)
    @echo "All images published!"
    @echo ""
    @echo "Frontend: {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:{{version}}"
    @echo "Frontend: {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:latest"
    @echo "Backend:  {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:{{version}}"
    @echo "Backend:  {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:latest"

# ========================================
# UTILITY TASKS
# ========================================

# Remove local Docker images
docker-clean:
    @echo "Removing local Docker images..."
    docker images --format {{ '{{.ID}} {{.Repository}}' }} | grep echophase | awk '{print $1}' | xargs docker rmi -f || true
    @echo "Docker images removed!"

# Show Docker configuration
docker-info:
    @echo "Docker Configuration:"
    @echo "  Registry:        {{DOCKER_REGISTRY}}"
    @echo "  Frontend Image:  {{FRONTEND_IMAGE}}"
    @echo "  Backend Image:   {{BACKEND_IMAGE}}"
    @echo "  Default Version: {{DEFAULT_VERSION}}"
    @echo ""
    @echo "Frontend workdir: ./frontend"
    @echo "Backend workdir:  ./src"
    @echo ""
    @echo "Full image names:"
    @echo "  {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:{{DEFAULT_VERSION}}"
    @echo "  {{DOCKER_REGISTRY}}/{{FRONTEND_IMAGE}}:latest"
    @echo "  {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:{{DEFAULT_VERSION}}"
    @echo "  {{DOCKER_REGISTRY}}/{{BACKEND_IMAGE}}:latest"
