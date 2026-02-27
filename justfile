# Justfile - main task runner configuration

# Load environment variables from .env file
set dotenv-load

# Import docker-specific tasks
import 'just/docker.just'

# Import helm-specific tasks
import 'just/helm.just'

# Show available commands
default:
    @just --list

# Run tests
test:
    dotnet test

# Run tests with detailed output
test-verbose:
    dotnet test --logger:"console;verbosity=detailed"

# Run all checks before commit
check: docker-build-frontend docker-build-backend
    @echo "All checks passed!"

# Clean up local artifacts
clean:
    @echo "Cleaning up..."
    @rm -rf frontend/dist/ frontend/build/ frontend/node_modules/.cache/
    @rm -rf src/bin/ src/obj/
    @echo "Clean complete!"

# Full rebuild: clean registry, rebuild and redeploy
rebuild: docker-clean helm-uninstall docker-publish-all helm-upgrade
    @echo "Full rebuild complete!"
