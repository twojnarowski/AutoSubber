# Environment Variables Configuration

This file documents how to configure the application using environment variables for secure secrets management.

## Authentication Secrets

### Google OAuth Configuration

Set these environment variables to configure Google OAuth:

```bash
export Authentication__Google__ClientId="your-google-client-id.apps.googleusercontent.com"
export Authentication__Google__ClientSecret="your-google-client-secret"
```

### Microsoft OAuth Configuration  

Set these environment variables to configure Microsoft OAuth:

```bash
export Authentication__Microsoft__ClientId="your-microsoft-client-id"
export Authentication__Microsoft__ClientSecret="your-microsoft-client-secret"
```

## Database Configuration

### Development (SQLite)
```bash
export ConnectionStrings__DefaultConnection="Data Source=autosubber.db"
export DatabaseProvider="SQLite"
```

### Production (SQL Server)
```bash
export ConnectionStrings__DefaultConnection="Server=your-server;Database=your-database;User Id=your-user;Password=your-password;"
export DatabaseProvider="SqlServer"
```

### Production (PostgreSQL)
```bash
export ConnectionStrings__PostgreSQLConnection="Host=your-host;Database=your-database;Username=your-user;Password=your-password;"
export DatabaseProvider="PostgreSQL"
```

## Data Protection (Production)

Configure where Data Protection keys are persisted:

```bash
export DataProtection__KeyDirectory="/var/keys"
export DataProtection__ApplicationName="AutoSubber"
```

## Running with Environment Variables

### Development
```bash
# Set your Google OAuth credentials
export Authentication__Google__ClientId="your-client-id"
export Authentication__Google__ClientSecret="your-client-secret"

# Run the application
cd AutoSubber/AutoSubber
dotnet run
```

### Production
```bash
# Set all required environment variables
export ASPNETCORE_ENVIRONMENT="Production"
export Authentication__Google__ClientId="your-client-id"
export Authentication__Google__ClientSecret="your-client-secret"
export ConnectionStrings__DefaultConnection="your-production-connection-string"
export DataProtection__KeyDirectory="/var/keys"

# Run the application
dotnet run
```

### Using Docker
```bash
docker run -e Authentication__Google__ClientId="your-client-id" \
           -e Authentication__Google__ClientSecret="your-client-secret" \
           -e DataProtection__KeyDirectory="/var/keys" \
           -v /host/keys:/var/keys \
           autosubber:latest
```

## Azure Key Vault (Optional)

For enhanced security in Azure deployments, you can use Azure Key Vault:

1. Install the Azure Key Vault configuration provider:
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

2. Configure in Program.cs:
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

3. Store secrets in Key Vault with names like:
   - `Authentication--Google--ClientId`
   - `Authentication--Google--ClientSecret`
   - `ConnectionStrings--DefaultConnection`

## Security Notes

- Never commit actual secrets to source control
- Use strong, unique passwords for production databases
- Regularly rotate OAuth client secrets
- Ensure the Data Protection key directory has proper permissions (readable only by the application user)
- In production, consider using managed identity services (Azure Managed Identity, AWS IAM roles, etc.)