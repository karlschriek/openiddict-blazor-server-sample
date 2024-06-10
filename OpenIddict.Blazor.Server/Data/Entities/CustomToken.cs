using OpenIddict.EntityFrameworkCore.Models;


namespace OpenIddict.Blazor.Server.Data;

public class CustomToken : OpenIddictEntityFrameworkCoreToken<string, CustomApplication, CustomAuthorization> {
    
    public virtual bool? IsDeclarativelyConfigured { get; set; }
}