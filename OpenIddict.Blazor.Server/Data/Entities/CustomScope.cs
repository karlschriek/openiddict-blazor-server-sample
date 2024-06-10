using OpenIddict.EntityFrameworkCore.Models;

namespace OpenIddict.Blazor.Server.Data;

public class CustomScope : OpenIddictEntityFrameworkCoreScope<string>{
    
    public virtual bool? IsDeclarativelyConfigured { get; set; }
}
