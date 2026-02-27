# helm.just - Helm-specific tasks

HELM_RELEASE := env_var_or_default('HELM_RELEASE', 'echophase')
HELM_NAMESPACE := env_var_or_default('HELM_NAMESPACE', 'echophase')
HELM_VALUES := env_var_or_default('HELM_VALUES', 'values.production.yaml')
HELM_DIR := env_var_or_default('HELM_DIR', 'helm')

# Install helm chart
helm-install:
    helm install {{HELM_RELEASE}} {{HELM_DIR}} \
        --namespace {{HELM_NAMESPACE}} \
        --values {{HELM_DIR}}/{{HELM_VALUES}} \
        --create-namespace

# Upgrade or install helm chart
helm-upgrade:
    helm upgrade --install {{HELM_RELEASE}} {{HELM_DIR}} \
        --namespace {{HELM_NAMESPACE}} \
        --values {{HELM_DIR}}/{{HELM_VALUES}} \
        --create-namespace
    kubectl rollout restart deployment -n {{HELM_NAMESPACE}}

# Uninstall helm release and delete namespace
helm-uninstall:
    helm uninstall {{HELM_RELEASE}} --namespace {{HELM_NAMESPACE}} --ignore-not-found
    kubectl delete namespace {{HELM_NAMESPACE}} --ignore-not-found
