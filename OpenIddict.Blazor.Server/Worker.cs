
using OpenIddict.Abstractions;
using OpenIddict.Blazor.Server.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIddict.Blazor.Server;

public class Worker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public Worker(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        
        await using var asyncServiceScope = _serviceProvider.CreateAsyncScope();

        var context = asyncServiceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync(); // this enables migations
        
        await CreateApplicationsAsync(asyncServiceScope);
        await CreateScopesAsync(asyncServiceScope);
    }

    async Task CreateApplicationsAsync(AsyncServiceScope asyncServiceScope)
    {
        var manager = asyncServiceScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        
        var authorizedClients = _configuration.GetSection("SimpleDbSeeding:Applications").Get<List<AuthorizedClient>>();
 
        if (authorizedClients == null)
        {
            return;
        }
        foreach (var client in authorizedClients)
        {
            
            var additionalScopes = client.Scopes;
            var appDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                ClientSecret = client.ClientSecret,
                ConsentType = client.ConsentType,
                DisplayName = client.DisplayName,
                RedirectUris =
                {
                    new Uri(client.LoginRedirectUri)
                },
                PostLogoutRedirectUris =
                {
                    new Uri(client.LogoutRedirectUri)
                },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Logout,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                },
                Requirements =
                {
                    Requirements.Features.ProofKeyForCodeExchange
                }
            };
            foreach (var scope in additionalScopes)
            {
                appDescriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            }
            
            
            if (await manager.FindByClientIdAsync(client.ClientId) == null) 
            {
                await manager.CreateAsync(appDescriptor);
            }
            // TODO currently updates do not work!
            // else
            // {
            //     await manager.UpdateAsync(appDescriptor);
            // }

        }
        
    }
    
    async Task CreateScopesAsync(AsyncServiceScope asyncServiceScope)
    {
        var manager = asyncServiceScope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        
        var scopesDictionary = _configuration.GetSection("SimpleDbSeeding:Scopes").Get<Dictionary<string, string[]>>();
        
        if (scopesDictionary == null)
        {
            return;
        }

        foreach (var (scope, resources) in scopesDictionary)
        {
            if (await manager.FindByNameAsync(scope) is null)
            {
                var descriptor = new OpenIddictScopeDescriptor
                {
                    Name = scope
                };
                foreach (var resource in resources)
                {
                    descriptor.Resources.Add(resource);
                }
                await manager.CreateAsync(descriptor);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


class AuthorizedClient
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string ConsentType { get; set; }
    public string DisplayName { get; set; }
    public string LoginRedirectUri { get; set; }
    public string LogoutRedirectUri { get; set; }
   
    
    public List<string> Scopes { get; set; }
}