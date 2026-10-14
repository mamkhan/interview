#!/usr/bin/env bash
# One-off per subscription: creates the Terraform state backend that root.hcl names, and grants
# Storage Blob Data Contributor (data-plane RBAC; the backend uses use_azuread_auth) to the
# signed-in user and, when DEPLOYER_OBJECT_ID is set, to the pipeline's service principal.
# Names per ADR-009 for the subscription's primary environment. Idempotent: safe to re-run.
set -euo pipefail

ENVIRONMENT="${1:-dev}"
LOCATION="${2:-northeurope}"
RG="acm${ENVIRONMENT}platrgtfstate"
ACCOUNT="acm${ENVIRONMENT}platsttfstate"
CONTAINER="tfstate"

SUBSCRIPTION="$(az account show --query id -o tsv)"
echo "Bootstrapping state backend $ACCOUNT in subscription $SUBSCRIPTION ($LOCATION)"

az group create -n "$RG" -l "$LOCATION" \
  --tags domain=Platform environment="$ENVIRONMENT" layer=platform managed-by=bootstrap -o none
az storage account create -n "$ACCOUNT" -g "$RG" -l "$LOCATION" \
  --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 \
  --allow-blob-public-access false \
  --tags domain=Platform environment="$ENVIRONMENT" layer=platform managed-by=bootstrap -o none
# Management-plane container creation: works before any data-plane role assignment propagates.
az storage container-rm create --storage-account "$ACCOUNT" -g "$RG" --name "$CONTAINER" -o none

SCOPE="/subscriptions/$SUBSCRIPTION/resourceGroups/$RG/providers/Microsoft.Storage/storageAccounts/$ACCOUNT"
ME="$(az ad signed-in-user show --query id -o tsv)"
for principal in "$ME" "${DEPLOYER_OBJECT_ID:-}"; do
  if [ -n "$principal" ]; then
    az role assignment create --assignee "$principal" \
      --role "Storage Blob Data Contributor" --scope "$SCOPE" -o none 2>/dev/null \
      || echo "role assignment for $principal already exists"
  fi
done
echo "State backend ready: $ACCOUNT/$CONTAINER"
