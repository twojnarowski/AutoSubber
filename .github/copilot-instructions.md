# AutoSubber
AutoSubber is a .NET 9.0 Blazor Server application with WebAssembly client components, featuring ASP.NET Core Identity authentication and SQL Server LocalDB storage. It's a web application that provides user authentication and can be extended for subscription management functionality.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Prerequisites and Setup
- Install .NET 9.0 SDK (REQUIRED - .NET 8.0 will not work):
  - `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 9.0.103`
  - `export PATH="$HOME/.dotnet:$PATH"`
  - `export DOTNET_ROOT="$HOME/.dotnet"`
  - Verify: `dotnet --version` should show 9.0.103 or higher

### Build and Development Commands
- **Package Restoration**: `dotnet restore` -- takes 1-32 seconds (faster on subsequent runs). NEVER CANCEL. Set timeout to 60+ minutes.
- **Build**: `dotnet build` -- takes 4-17 seconds (faster on subsequent runs). NEVER CANCEL. Set timeout to 30+ minutes.
- **Run Application**: `dotnet run` (from AutoSubber/AutoSubber/AutoSubber directory) -- starts web server on http://localhost:5143
- **Test**: `dotnet test` -- No tests currently exist in the repository

### Running the Application
- ALWAYS run from the server project directory: `cd AutoSubber/AutoSubber/AutoSubber`
- Default URLs: http://localhost:5143 (HTTP), https://localhost:7029 (HTTPS)
- The application will start with warnings about unencrypted data protection keys (normal for development)
- Build occurs automatically when running if needed

## Validation
- ALWAYS manually validate the application by navigating to http://localhost:5143 after making changes
- Test the authentication flow:
  1. Navigate to `/Account/Login` to verify login page loads
  2. Navigate to `/Account/Register` to verify registration page loads
  3. Verify the homepage shows "Hello, world!" and "Welcome to your new app."
- ALWAYS run through at least one complete user scenario after making changes
- The application can be built and run successfully - always build and exercise your changes

## Project Structure
The solution contains two main projects:
- **AutoSubber** (Server): ASP.NET Core Blazor Server application (.NET 9.0)
  - Main entry point: `Program.cs`
  - Database context: `Data/ApplicationDbContext.cs`
  - Identity configuration with Entity Framework Core
  - Components in `Components/` directory
- **AutoSubber.Client** (Client): Blazor WebAssembly client (.NET 9.0)
  - Client-side components and logic
  - Referenced by the server project

### Key Directories and Files
```
AutoSubber.sln                              # Solution file
AutoSubber/AutoSubber/
├── AutoSubber.csproj                       # Server project file
├── Program.cs                              # Application entry point
├── appsettings.json                        # Configuration (LocalDB connection)
├── Properties/launchSettings.json          # Launch configuration
├── Data/
│   ├── ApplicationDbContext.cs             # EF Core database context
│   ├── ApplicationUser.cs                  # Identity user model
│   └── Migrations/                         # Database migrations
├── Components/
│   ├── Account/                            # Authentication components
│   ├── Layout/                             # Layout components
│   ├── Pages/                              # Blazor pages
│   └── App.razor                           # Main app component
└── wwwroot/                                # Static web assets

AutoSubber/AutoSubber.Client/
├── AutoSubber.Client.csproj                # Client project file
├── Program.cs                              # WebAssembly entry point
└── RedirectToLogin.razor                   # Client authentication redirect
```

## Database and Entity Framework
- Uses SQL Server LocalDB: `(localdb)\\mssqllocaldb`
- Database name: `aspnet-AutoSubber-620e5ad8-0a6e-4c35-a03e-fb1027313b3d`
- Identity migrations are included in `Data/Migrations/`
- EF Core tools may not be installed by default
- The application will create the database automatically on first run

## Common Tasks
The following information helps avoid unnecessary exploration:

### Solution Structure
```
ls -la [repo-root]
.git/
.gitattributes
.gitignore                                  # Visual Studio .gitignore
AutoSubber/                                 # Projects directory
AutoSubber.sln                              # Solution file
```

### Package Dependencies
Key packages used:
- Microsoft.AspNetCore.Components.WebAssembly.Server 9.0.7
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.7
- Microsoft.EntityFrameworkCore.SqlServer 9.0.7
- Microsoft.EntityFrameworkCore.Tools 9.0.7

### Configuration Files
- `appsettings.json`: Contains database connection string and logging configuration
- `Properties/launchSettings.json`: Defines development server ports and environment variables
- Uses LocalDB connection string by default

## Development Workflow
1. **Setup**: Install .NET 9.0 SDK and set environment variables
2. **Restore**: Run `dotnet restore` (32 seconds)
3. **Build**: Run `dotnet build` (17 seconds) 
4. **Run**: Execute `dotnet run` from `AutoSubber/AutoSubber` directory
5. **Test**: Navigate to http://localhost:5143 and test authentication pages
6. **Validate**: Always test login/register functionality after changes

## Troubleshooting
- **SDK Errors**: Ensure .NET 9.0 SDK is installed and PATH is set correctly
- **Build Failures**: Check that all package references are restored
- **Database Issues**: LocalDB should auto-create; no manual database setup required
- **Port Conflicts**: Default ports are 5143 (HTTP) and 7029 (HTTPS)

## Timing Expectations
- **NEVER CANCEL**: Restore takes 1-32 seconds, build takes 4-17 seconds. Use timeouts of 60+ minutes for safety.
- Package restoration: 1-32 seconds (varies with network and cache)
- Full build: 4-17 seconds (faster on incremental builds)
- Application startup: ~5 seconds
- Page load times: < 1 second for basic pages