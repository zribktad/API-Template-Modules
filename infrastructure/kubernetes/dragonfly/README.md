# DragonFly on Kubernetes

## Prerequisites

Install the DragonFly Kubernetes operator via Helm:

```bash
helm repo add dragonfly https://dragonflydb.github.io/dragonfly-operator/
helm repo update
helm install dragonfly-operator dragonfly/dragonfly-operator --namespace dragonfly-operator-system --create-namespace
```

## Deploy

```bash
kubectl create namespace apitemplate
kubectl apply -f dragonfly.yml
```

## Connection

The API connects to DragonFly via the operator-managed service:

```
dragonfly.apitemplate.svc.cluster.local:6379
```

Set this as the `Dragonfly__ConnectionString` environment variable in your API deployment.

## How It Works

The DragonFly operator manages:

- **Automatic failover** — if the master pod fails, the operator promotes a replica to master
- **Replica management** — maintains the desired replica count
- **Rolling updates** — zero-downtime upgrades when the DragonFly image version changes

No HAProxy is needed in Kubernetes — the operator handles service routing internally.
