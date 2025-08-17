# Deployment Guide

This document outlines the deployment setup for AutoSubber, including Docker containerization and CI/CD pipeline.

## Quick Start

### Local Development with Docker

1. **Prerequisites**
   - Docker and Docker Compose installed
   - .NET 9.0 SDK (for local development)

2. **Configure environment variables**
   ```bash
   cp .env.example .env
   # Edit .env with your OAuth credentials
   ```

3. **Run with Docker Compose**
   ```bash
   # SQLite version (simplest)
   docker-compose up autosubber
   
   # PostgreSQL version (production-like)
   docker-compose --profile postgres up
   ```

4. **Access the application**
   - SQLite version: http://localhost:8080
   - PostgreSQL version: http://localhost:8081
   - Health check: http://localhost:8080/health

## Production Deployment

### Container Registry

The CI/CD pipeline automatically builds and pushes Docker images to GitHub Container Registry:
- `ghcr.io/twojnarowski/autosubber:latest` - Latest main branch
- `ghcr.io/twojnarowski/autosubber:commit-<sha>` - Specific commits

### Environment Variables

For production deployment, configure these environment variables:

#### Required
```bash
# OAuth Configuration
AUTHENTICATION__GOOGLE__CLIENTID=your-google-client-id
AUTHENTICATION__GOOGLE__CLIENTSECRET=your-google-client-secret

# Database
DATABASEPROVIDER=PostgreSQL  # or SQLite, SqlServer
CONNECTIONSTRINGS__POSTGRESQLCONNECTION=Host=db;Database=autosubber;Username=user;Password=pass

# Security
DATAPROTECTION__KEYDIRECTORY=/var/keys
ASPNETCORE_ENVIRONMENT=Production
```

#### Optional
```bash
# Microsoft OAuth
AUTHENTICATION__MICROSOFT__CLIENTID=your-microsoft-client-id
AUTHENTICATION__MICROSOFT__CLIENTSECRET=your-microsoft-client-secret

# HTTPS
ASPNETCORE_URLS=https://+:443;http://+:80
ASPNETCORE_Kestrel__Certificates__Default__Path=/path/to/certificate.pfx
ASPNETCORE_Kestrel__Certificates__Default__Password=certificate-password
```

### Cloud Deployment Examples

#### Docker Compose (Simple)
```yaml
version: '3.8'
services:
  autosubber:
    image: ghcr.io/twojnarowski/autosubber:latest
    ports:
      - "80:8080"
      - "443:8080"  # with reverse proxy
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DatabaseProvider=PostgreSQL
      - ConnectionStrings__PostgreSQLConnection=Host=db;Database=autosubber;Username=autosubber;Password=secure_password
      - Authentication__Google__ClientId=${GOOGLE_CLIENT_ID}
      - Authentication__Google__ClientSecret=${GOOGLE_CLIENT_SECRET}
    volumes:
      - data_protection_keys:/var/keys
    restart: unless-stopped
    
  db:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: autosubber
      POSTGRES_USER: autosubber
      POSTGRES_PASSWORD: secure_password
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped

volumes:
  data_protection_keys:
  postgres_data:
```

#### Azure Container Instances
```bash
az container create \
  --resource-group myResourceGroup \
  --name autosubber \
  --image ghcr.io/twojnarowski/autosubber:latest \
  --ports 80 443 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    DatabaseProvider=PostgreSQL \
    ConnectionStrings__PostgreSQLConnection="Host=mydb.postgres.database.azure.com;Database=autosubber;Username=autosubber;Password=mypassword" \
  --secure-environment-variables \
    Authentication__Google__ClientId="$GOOGLE_CLIENT_ID" \
    Authentication__Google__ClientSecret="$GOOGLE_CLIENT_SECRET" \
  --dns-name-label autosubber-app
```

#### AWS ECS/Fargate
```json
{
  "family": "autosubber",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "containerDefinitions": [
    {
      "name": "autosubber",
      "image": "ghcr.io/twojnarowski/autosubber:latest",
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        },
        {
          "name": "DatabaseProvider",
          "value": "PostgreSQL"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__PostgreSQLConnection",
          "valueFrom": "arn:aws:secretsmanager:region:account:secret:autosubber/db"
        },
        {
          "name": "Authentication__Google__ClientId",
          "valueFrom": "arn:aws:secretsmanager:region:account:secret:autosubber/google-client-id"
        }
      ]
    }
  ]
}
```

## CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) automatically:

1. **Tests** - Runs unit tests on every push/PR
2. **Builds** - Creates Docker image with proper tags
3. **Publishes** - Pushes to GitHub Container Registry
4. **Security Scans** - Runs Trivy vulnerability scanner
5. **Deploys** - Ready for deployment step integration

### Customizing Deployment

To add actual deployment (not just the placeholder), modify the `deploy-dev` job in `.github/workflows/ci-cd.yml`:

```yaml
deploy-dev:
  runs-on: ubuntu-latest
  needs: build-and-push
  if: github.ref == 'refs/heads/main'
  environment: development
  
  steps:
  - name: Deploy to Azure
    run: |
      # Azure CLI commands
      az container restart --name autosubber --resource-group myRG
      
  - name: Deploy to AWS
    run: |
      # AWS CLI commands
      aws ecs update-service --cluster my-cluster --service autosubber
      
  - name: Deploy to GCP
    run: |
      # gcloud commands
      gcloud run deploy autosubber --image ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:latest
```

## Security Considerations

1. **Secrets Management** - Use cloud provider secret management services
2. **HTTPS** - Always use HTTPS in production
3. **Data Protection Keys** - Ensure persistent storage for encryption keys
4. **Database Security** - Use encrypted connections and limited permissions
5. **Container Security** - Runs as non-root user, includes vulnerability scanning

## Monitoring and Health Checks

- **Health Endpoint**: `/health` - Returns 200 OK when application is healthy
- **Logs**: Application uses Serilog for structured logging
- **Metrics**: Ready for integration with monitoring solutions

## Troubleshooting

### Common Issues

1. **OAuth Issues**
   - Verify redirect URIs match deployment domain
   - Check client ID/secret configuration
   
2. **Database Connection**
   - Verify connection string format
   - Check network connectivity and firewall rules
   
3. **Container Startup**
   - Check logs: `docker logs <container-id>`
   - Verify environment variables
   - Test health endpoint

### Debug Mode

For debugging, run container with debug logging:
```bash
docker run -e ASPNETCORE_ENVIRONMENT=Development ghcr.io/twojnarowski/autosubber:latest
```