using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Web.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ClaimsCurrentUser(accessor);
