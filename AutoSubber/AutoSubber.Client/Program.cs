using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace AutoSubber.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddAuthorizationCore();
            builder.Services.AddCascadingAuthenticationState();

            await builder.Build().RunAsync();
        }
    }
}
