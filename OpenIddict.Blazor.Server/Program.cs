using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Blazor.Server.Components;
using OpenIddict.Blazor.Server.Components.Account;
using OpenIddict.Blazor.Server.Data;
using System.Security.Cryptography;
using Quartz;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OpenIddict.Blazor.Server;

////////////////////////////////////////////////////////////////////////////////////////////////
// services
////////////////////////////////////////////////////////////////////////////////////////////////

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});
    //.AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options => {
        options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure());

        options.UseOpenIddict();
        options.UseOpenIddict<CustomApplication, CustomAuthorization, CustomScope, CustomToken, string>();
    }
);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


 builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
     .AddEntityFrameworkStores<ApplicationDbContext>()
     .AddSignInManager()
     .AddDefaultTokenProviders();

// builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
//     .AddEntityFrameworkStores<ApplicationDbContext>()
//     .AddSignInManager()
//     .AddDefaultTokenProviders();

// Bind options for "EnableAuthenticator" page
builder.Services.Configure<EnableAuthenticatorOptions>(builder.Configuration.GetSection("EnableAuthenticator:Options"));

// Set up "EmailSender"
if (builder.Configuration["EmailSender:Name"] == "SendGrid")
{
    builder.Services.Configure<SendGridEmailSenderOptions>(builder.Configuration.GetSection("EmailSender:Options"));
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SendGridEmailSender>();
}
else if (builder.Configuration["EmailSender:Name"] == "NoOp")
{
    //NoOp sender will not perform any actual email sends
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
}
else
{
    throw new Exception(
        "You must configure at least one email sender. For production use `SendGrid`. For dev you may also choose to use `NoOp` instead");
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
        
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});


////////////////////////////////////////////////////////////////////////////////////////////////
// services (related to OpenIddict specifically)
////////////////////////////////////////////////////////////////////////////////////////////////

// Add RsaPrivateKey. It's required to this with depedency injection. If you don't do this, the RSA instance will be prematurely disposed.
builder.Services.AddSingleton<RsaSecurityKey>(provider =>
{
    RSA rsa = RSA.Create();
    rsa.ImportFromPem(builder.Configuration["JwtSigning:RsaPrivateKey"]);
    return new RsaSecurityKey(rsa);
});

builder.Services.AddSingleton(provider =>
{
    RSA clientPrivateKey = RSA.Create();
    clientPrivateKey.ImportFromPem(builder.Configuration["IntegratedClient:JwtSigning:RsaPrivateKey"]);

    RSA serverPrivateKey = RSA.Create();
    serverPrivateKey.ImportFromPem(builder.Configuration["Server:JwtSigning:RsaPrivateKey"]);
    //publicKey.ImportFromPem(privateKey.ExportRSAPublicKeyPem());
    return new Tuple<RsaSecurityKey, RsaSecurityKey>(
        new RsaSecurityKey(clientPrivateKey),
        new RsaSecurityKey(serverPrivateKey)
    );
});

// OpenIddict offers native integration with Quartz.NET to perform scheduled tasks (like pruning orphaned
// authorizations/tokens from the database) at regular intervals.
builder.Services.AddQuartz(options =>
{
    options.UseMicrosoftDependencyInjectionJobFactory();
    options.UseSimpleTypeLoader();
    options.UseInMemoryStore();
});

// Register the Quartz.NET service and configure it to block shutdown until jobs are complete.
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

// Add OpenIdDict
builder.Services.AddOpenIddict()
    // Register the OpenIddict core components.
    .AddCore(options =>
    {
        // Configure OpenIddict to use the Entity Framework Core stores and models.
        
        options.UseEntityFrameworkCore()
               .ReplaceDefaultEntities<CustomApplication, CustomAuthorization, CustomScope, CustomToken, string>() 
               .UseDbContext<ApplicationDbContext>();

        // Enable Quartz.NET integration.
        options.UseQuartz();
    })

    // Register the OpenIddict client components.
    .AddClient(options =>
    {
        // Note: this sample uses the code flow, but you can enable the other flows if necessary.
        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();

        var signingKey = builder.Services.BuildServiceProvider().
            GetRequiredService<Tuple<RsaSecurityKey, RsaSecurityKey>>().Item1;
        options.AddSigningKey(signingKey);

        var encryptionKeyBase64 = SymmetricKeyUtils.ExtractBase64FromPem(builder.Configuration["IntegratedClient:JwtEncryption:SymmetricKey"]);
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(encryptionKeyBase64)));

        // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
        options.UseAspNetCore()
            .EnableStatusCodePagesIntegration()
            .EnableRedirectionEndpointPassthrough();
        
        // Register the System.Net.Http integration and use the identity of the current
        // assembly as a more specific user agent, which can be useful when dealing with
        // providers that use the user agent as a way to throttle requests (e.g Reddit).
        options.UseSystemNetHttp();
               //.SetProductInformation(typeof(Startup).Assembly);
               
        // Register the Web providers integrations.
        //
        // Note: to mitigate mix-up attacks, it's recommended to use a unique redirection endpoint
        // URI per provider, unless all the registered providers support returning a special "iss"
        // parameter containing their URL as part of authorization responses. For more information,
        // see https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics#section-4.4.
        options.UseWebProviders()
            .AddMicrosoft(options =>
                {
                    options.SetClientId(builder.Configuration["IntegratedClient:AzureAd:ClientId"]);
                    options.SetClientSecret(builder.Configuration["IntegratedClient:AzureAd:ClientCredentials:0:ClientSecret"]);
                    options.SetTenant(builder.Configuration["IntegratedClient:AzureAd:TenantId"]);
                    options.SetRedirectUri(builder.Configuration["IntegratedClient:AzureAd:RedirectUri"]);
                }
            );

    })
    
    // Register the OpenIddict server components.
    .AddServer(options =>
    {
        // Enable the authorization, logout, token and userinfo endpoints.
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetLogoutEndpointUris("connect/logout")
            .SetIntrospectionEndpointUris("connect/introspect")
            .SetTokenEndpointUris("connect/token")
            .SetUserinfoEndpointUris("connect/userinfo");

        // Mark the "email", "profile" and "roles" scopes as supported scopes.
        var defaultScopes = new[] { Scopes.Email, Scopes.Profile, Scopes.Roles };
        var additionalScopes = builder.Configuration
            .GetSection("Server:AdditionalScopes")
            .GetChildren()
            .Select(x => x.Key);

        options.RegisterScopes(defaultScopes.Concat(additionalScopes).ToArray());
        options.RegisterClaims(Claims.Role);
        options.RegisterClaims(Claims.ClientId);
        options.RegisterClaims(Claims.Issuer);

        // Note: this sample only uses the authorization code flow but you can enable
        // the other flows if you need to support implicit, password or client credentials.
        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();

        // Register the signing and encryption credentials.
        var signingKey = builder.Services.BuildServiceProvider().
            GetRequiredService<Tuple<RsaSecurityKey, RsaSecurityKey>>().Item2;
        options.AddSigningKey(signingKey);

        var encryptionKeyBase64 = SymmetricKeyUtils.ExtractBase64FromPem(builder.Configuration["Server:JwtEncryption:SymmetricKey"]);
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(encryptionKeyBase64)));


        // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableLogoutEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserinfoEndpointPassthrough()
               .EnableStatusCodePagesIntegration();
    })

    // Register the OpenIddict validation components.
    .AddValidation(options =>
    {
        // Import the configuration from the local OpenIddict server instance.
        options.UseLocalServer();

        // Register the ASP.NET Core host.
        options.UseAspNetCore();
    });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<Worker>(); 
}


////////////////////////////////////////////////////////////////////////////////////////////////
// app
////////////////////////////////////////////////////////////////////////////////////////////////

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.UseRouting();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapDefaultControllerRoute();
    endpoints.MapRazorPages();
});

app.Run();