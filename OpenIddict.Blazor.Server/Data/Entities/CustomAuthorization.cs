using OpenIddict.EntityFrameworkCore.Models;


namespace OpenIddict.Blazor.Server.Data;


public class CustomAuthorization : OpenIddictEntityFrameworkCoreAuthorization<string, CustomApplication, CustomToken>
{
    public virtual bool? IsDeclarativelyConfigured { get; set; }
}

