# Production Deployment Security Guide

This guide outlines the security configurations required for production deployment of AutoSubber.

## Data Protection Key Persistence

AutoSubber uses ASP.NET Core Data Protection to encrypt sensitive tokens (Google OAuth refresh tokens, etc.) before storing them in the database. In production environments, you need to ensure Data Protection keys persist across application restarts and deployments.

### Recommended Approaches

#### 1. File System Persistence (Linux/Windows)

Create a persistent directory for keys:
```bash
sudo mkdir -p /var/keys
sudo chown app-user:app-user /var/keys
sudo chmod 700 /var/keys
```

Set environment variable:
```bash
export DataProtection__KeyDirectory="/var/keys"
```

#### 2. Redis Persistence (Recommended for scaled deployments)

Add package:
```bash
dotnet add package Microsoft.AspNetCore.DataProtection.StackExchangeRedis
```

Configure in Program.cs:
```csharp
if (builder.Environment.IsProduction())
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnection))
    {
        builder.Services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect(redisConnection));
    }
}
```

#### 3. Azure Key Vault (Azure deployments)

Add package:
```bash
dotnet add package Azure.Extensions.AspNetCore.DataProtection.Keys
```

Configure in Program.cs:
```csharp
if (builder.Environment.IsProduction())
{
    var keyVaultUri = builder.Configuration["DataProtection:AzureKeyVault:KeyVaultUri"];
    var keyName = builder.Configuration["DataProtection:AzureKeyVault:KeyName"];
    
    if (!string.IsNullOrEmpty(keyVaultUri) && !string.IsNullOrEmpty(keyName))
    {
        builder.Services.AddDataProtection()
            .PersistKeysToAzureBlobStorage(/* Azure Blob config */)
            .ProtectKeysWithAzureKeyVault(new Uri(keyVaultUri), keyName, new DefaultAzureCredential());
    }
}
```

#### 4. SQL Server Persistence

Add package:
```bash
dotnet add package Microsoft.AspNetCore.DataProtection.EntityFrameworkCore
```

Configure in Program.cs:
```csharp
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DataProtectionKeysContext>();
```

## OAuth Secrets Management

### Environment Variables (Recommended)

Never commit OAuth secrets to source control. Use environment variables:

```bash
# Google OAuth
export Authentication__Google__ClientId="your-google-client-id.apps.googleusercontent.com"
export Authentication__Google__ClientSecret="your-google-client-secret"

# Microsoft OAuth  
export Authentication__Microsoft__ClientId="your-microsoft-client-id"
export Authentication__Microsoft__ClientSecret="your-microsoft-client-secret"
```

### Azure Key Vault Integration

For Azure deployments, store secrets in Key Vault:

1. Create secrets in Azure Key Vault:
   - `Authentication--Google--ClientId`
   - `Authentication--Google--ClientSecret`
   - `Authentication--Microsoft--ClientId`
   - `Authentication--Microsoft--ClientSecret`

2. Configure Key Vault in Program.cs:
```csharp
if (builder.Environment.IsProduction())
{
    var keyVaultEndpoint = builder.Configuration["KeyVaultEndpoint"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential());
    }
}
```

### AWS Secrets Manager

For AWS deployments:

1. Install package:
```bash
dotnet add package Amazon.Extensions.Configuration.SystemsManager
```

2. Configure in Program.cs:
```csharp
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddSystemsManager("/autosubber/");
}
```

## Database Security

### Connection String Security

Use secure connection strings with encrypted connections:

#### SQL Server
```bash
export ConnectionStrings__DefaultConnection="Server=your-server;Database=autosubber;User Id=autosubber_user;Password=complex-password;Encrypt=True;TrustServerCertificate=False;"
```

#### PostgreSQL  
```bash
export ConnectionStrings__PostgreSQLConnection="Host=your-host;Database=autosubber;Username=autosubber_user;Password=complex-password;SSL Mode=Require;"
```

### Database User Permissions

Create a dedicated database user with minimal permissions:

#### SQL Server
```sql
CREATE LOGIN [autosubber_user] WITH PASSWORD = 'complex-password';
CREATE USER [autosubber_user] FOR LOGIN [autosubber_user];
ALTER ROLE [db_datareader] ADD MEMBER [autosubber_user];
ALTER ROLE [db_datawriter] ADD MEMBER [autosubber_user];
-- Grant specific permissions as needed
```

#### PostgreSQL
```sql
CREATE USER autosubber_user WITH PASSWORD 'complex-password';
GRANT CONNECT ON DATABASE autosubber TO autosubber_user;
GRANT USAGE ON SCHEMA public TO autosubber_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO autosubber_user;
```

## HTTPS Configuration

### Production Certificate

Configure HTTPS certificate in production:

```bash
export ASPNETCORE_URLS="https://+:443;http://+:80"
export ASPNETCORE_Kestrel__Certificates__Default__Path="/path/to/certificate.pfx"
export ASPNETCORE_Kestrel__Certificates__Default__Password="certificate-password"
```

### Reverse Proxy Configuration

When using a reverse proxy (nginx, Apache, etc.), ensure:

1. HTTPS termination at the proxy
2. Secure headers are set
3. HTTP to HTTPS redirection

Example nginx configuration:
```nginx
server {
    listen 443 ssl http2;
    server_name your-domain.com;
    
    ssl_certificate /path/to/certificate.crt;
    ssl_certificate_key /path/to/private.key;
    
    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## Monitoring and Logging

### Secure Logging Configuration

Ensure logs don't contain sensitive information:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "AutoSubber": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/autosubber/autosubber-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### Security Monitoring

Monitor for:
- Failed authentication attempts
- Token encryption/decryption errors
- Data Protection key issues
- Unauthorized access attempts

## Deployment Checklist

- [ ] OAuth secrets configured via environment variables or Key Vault
- [ ] Data Protection keys persistence configured
- [ ] Database connection encrypted
- [ ] HTTPS properly configured
- [ ] Secure headers configured
- [ ] Logging configured without sensitive data
- [ ] Database user has minimal required permissions
- [ ] Regular security updates scheduled
- [ ] Backup strategy for Data Protection keys
- [ ] Monitoring and alerting configured

## Security Validation

After deployment, verify:

1. **Secrets not in source control**: `git log --all -p | grep -i "client.*secret"`
2. **Token encryption working**: Check database - tokens should be encrypted
3. **HTTPS enforced**: Access via HTTP should redirect to HTTPS
4. **Data Protection keys persistent**: Restart application, verify tokens still decrypt
5. **OAuth flow working**: Test Google/Microsoft authentication

## Incident Response

If secrets are compromised:

1. **Immediately rotate** all affected OAuth secrets
2. **Regenerate** Data Protection keys (will invalidate existing tokens)
3. **Audit** access logs for unauthorized access
4. **Update** all deployment configurations
5. **Force re-authentication** of all users if necessary