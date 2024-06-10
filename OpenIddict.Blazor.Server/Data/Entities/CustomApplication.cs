using OpenIddict.EntityFrameworkCore.Models;

namespace OpenIddict.Blazor.Server.Data;

public class CustomApplication : OpenIddictEntityFrameworkCoreApplication<string,
    CustomAuthorization, CustomToken>
{
    public virtual string? ClientSecretAkvUrl { get; set; }
    
    public virtual bool? IsDeclarativelyConfigured { get; set; }
    
}