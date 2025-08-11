# AutoSubber

AutoSubber is a .NET 9.0 Blazor Server application with WebAssembly client components that provides YouTube playlist management functionality through Google OAuth authentication and the YouTube Data API v3.

## Features

- **User Authentication**: ASP.NET Core Identity with Google OAuth 2.0 integration
- **YouTube Integration**: Access YouTube playlists and videos using YouTube Data API v3
- **Secure Token Management**: Encrypted storage of OAuth refresh tokens
- **Multi-Database Support**: SQL Server, PostgreSQL, and SQLite
- **Blazor Components**: Server-side and WebAssembly client components

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (version 9.0.103 or higher)
- Google Cloud Platform account
- YouTube Data API v3 enabled
- OAuth 2.0 credentials configured

## Google Cloud Setup

### Step 1: Create a Google Cloud Project

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Sign in with your Google account
3. Click on the project dropdown at the top of the page
4. Click **"New Project"**
5. Enter a project name (e.g., "AutoSubber")
6. Select your organization (if applicable)
7. Click **"Create"**
8. Wait for the project to be created and make sure it's selected

### Step 2: Enable YouTube Data API v3

1. In the Google Cloud Console, navigate to **"APIs & Services" > "Library"**
2. Search for "YouTube Data API v3"
3. Click on **"YouTube Data API v3"** from the results
4. Click **"Enable"** to enable the API for your project
5. Wait for the API to be enabled (this may take a few minutes)

### Step 3: Configure OAuth Consent Screen

1. Navigate to **"APIs & Services" > "OAuth consent screen"**
2. Select **"External"** as the user type (unless you have a Google Workspace account)
3. Click **"Create"**

#### OAuth Consent Screen Configuration:

**App Information:**
- **App name**: `AutoSubber`
- **User support email**: Your email address
- **App logo**: (Optional) Upload your app logo
- **App domain**: (Optional) Your app's homepage URL
- **Developer contact information**: Your email address

**Scopes:**
1. Click **"Add or Remove Scopes"**
2. Add the following YouTube scopes:
   - `https://www.googleapis.com/auth/youtube.force-ssl`
   - `https://www.googleapis.com/auth/youtube.readonly`
   - `https://www.googleapis.com/auth/youtube`
3. Click **"Update"**

**Test Users (for External apps in testing):**
1. Click **"Add Users"**
2. Add email addresses of users who will test the application
3. Click **"Save and Continue"**

**Summary:**
- Review your configuration
- Click **"Back to Dashboard"**

### Step 4: Create OAuth Client ID

1. Navigate to **"APIs & Services" > "Credentials"**
2. Click **"Create Credentials" > "OAuth client ID"**
3. Select **"Web application"** as the application type

#### OAuth Client Configuration:

**Name**: `AutoSubber Web Client`

**Authorized JavaScript origins**:
```
http://localhost:5143
https://localhost:7029
```

**Authorized redirect URIs**:
```
http://localhost:5143/signin-google
https://localhost:7029/signin-google
```

> **Note**: For production deployment, replace `localhost` URLs with your actual domain:
> - `https://yourdomain.com/signin-google`

4. Click **"Create"**
5. **Save your credentials**:
   - **Client ID**: Copy and save this value
   - **Client Secret**: Copy and save this value securely

### Step 5: Verify OAuth Consent Screen

1. Navigate back to **"APIs & Services" > "OAuth consent screen"**
2. Verify that your scopes include:
   - `https://www.googleapis.com/auth/youtube.force-ssl`
   - `https://www.googleapis.com/auth/youtube.readonly`
   - `https://www.googleapis.com/auth/youtube`
3. For production use, you may need to submit your app for verification

## Application Setup

### 1. Clone the Repository

```bash
git clone https://github.com/twojnarowski/AutoSubber.git
cd AutoSubber
```

### 2. Install .NET 9.0 SDK

Download and install the .NET 9.0 SDK from [Microsoft's website](https://dotnet.microsoft.com/download/dotnet/9.0).

Verify installation:
```bash
dotnet --version
```
Should output `9.0.103` or higher.

### 3. Configure OAuth Credentials

#### Option A: Environment Variables (Recommended)

Set the following environment variables with your Google OAuth credentials:

**Linux/macOS:**
```bash
export Authentication__Google__ClientId="your-google-client-id.apps.googleusercontent.com"
export Authentication__Google__ClientSecret="your-google-client-secret"
```

**Windows PowerShell:**
```powershell
$env:Authentication__Google__ClientId="your-google-client-id.apps.googleusercontent.com"
$env:Authentication__Google__ClientSecret="your-google-client-secret"
```

**Windows Command Prompt:**
```cmd
set Authentication__Google__ClientId=your-google-client-id.apps.googleusercontent.com
set Authentication__Google__ClientSecret=your-google-client-secret
```

#### Option B: User Secrets (Development)

Use .NET's Secret Manager for development:

```bash
cd AutoSubber/AutoSubber
dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
```

#### Option C: Configuration File (Not Recommended for Production)

Edit `appsettings.json` (for development only):

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    }
  }
}
```

> **⚠️ Warning**: Never commit OAuth secrets to source control. Use environment variables or user secrets.

### 4. Install Dependencies

```bash
dotnet restore
```

### 5. Build the Application

```bash
dotnet build
```

### 6. Run the Application

```bash
cd AutoSubber/AutoSubber
dotnet run
```

The application will start on:
- HTTP: http://localhost:5143
- HTTPS: https://localhost:7029

## Testing the Integration

1. Navigate to http://localhost:5143
2. Click **"Login"** in the top navigation
3. Click **"Google"** to test the OAuth flow
4. You should be redirected to Google's consent screen
5. Authorize the application to access your YouTube data
6. You should be redirected back to the application and logged in

## Configuration Details

### YouTube API Scopes

The application requests the following YouTube API scopes:

- `https://www.googleapis.com/auth/youtube.force-ssl`: Full access to YouTube account
- `https://www.googleapis.com/auth/youtube.readonly`: Read-only access to YouTube account  
- `https://www.googleapis.com/auth/youtube`: Manage YouTube account

### OAuth Configuration

The Google OAuth is configured in `Program.cs` with:
- **Callback Path**: `/signin-google`
- **Access Type**: `offline` (for refresh tokens)
- **Token Storage**: Enabled for API access

### Database Configuration

The application supports multiple database providers:

**SQLite (Default for development):**
```json
{
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=autosubber.db"
  },
  "DatabaseProvider": "SQLite"
}
```

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server;Database=autosubber;User Id=your-user;Password=your-password;"
  },
  "DatabaseProvider": "SqlServer"
}
```

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "PostgreSQLConnection": "Host=your-host;Database=autosubber;Username=your-user;Password=your-password;"
  },
  "DatabaseProvider": "PostgreSQL"
}
```

## Production Deployment

For production deployment, see the following guides:

- [Environment Variables Configuration](ENVIRONMENT_VARIABLES.md)
- [Production Security Guide](PRODUCTION_SECURITY.md)

### Important Production Considerations

1. **Update Redirect URIs**: Replace localhost URLs with your production domain
2. **OAuth Consent Screen Verification**: Submit your app for Google verification
3. **Secure Token Storage**: Configure proper Data Protection key persistence
4. **HTTPS**: Ensure your application runs over HTTPS
5. **Environment Variables**: Use secure methods to manage OAuth secrets

## Troubleshooting

### Common Issues

**"The OAuth client was not found" Error:**
- Verify your Client ID is correct
- Ensure the OAuth client is created for a "Web application"
- Check that your domain matches the authorized origins

**"Redirect URI mismatch" Error:**
- Verify redirect URIs in Google Cloud Console include `/signin-google`
- Ensure the domain and protocol (http/https) match exactly
- For localhost, include both HTTP and HTTPS versions

**"Access blocked" Error:**
- Ensure your email is added as a test user in the OAuth consent screen
- Verify all required scopes are added to the consent screen
- For production, submit your app for verification

**YouTube API quotas exceeded:**
- Check your API usage in Google Cloud Console
- Consider implementing caching to reduce API calls
- Request quota increases if needed

### Getting Help

If you encounter issues:

1. Check the application logs for detailed error messages
2. Verify your Google Cloud Console configuration
3. Ensure all environment variables are set correctly
4. Review the [Google OAuth 2.0 documentation](https://developers.google.com/identity/protocols/oauth2)

## Contributing

Please read our contributing guidelines before submitting pull requests.

## License

This project is licensed under the MIT License. See the LICENSE file for details.