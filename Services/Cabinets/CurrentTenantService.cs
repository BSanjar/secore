using WebApplication1.Helpers;

namespace WebApplication1.Services.Cabinets;

public sealed record CurrentTenantContext(
    bool IsAuthenticated,
    string? OrganizationId,
    string? OrganizationType,
    string? UserId,
    string? UserName,
    CabinetProfile Profile);

public interface ICurrentTenantService
{
    CurrentTenantContext GetCurrent();
}

public sealed class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICabinetProfileResolver _cabinetProfileResolver;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor, ICabinetProfileResolver cabinetProfileResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _cabinetProfileResolver = cabinetProfileResolver;
    }

    public CurrentTenantContext GetCurrent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new CurrentTenantContext(
                IsAuthenticated: false,
                OrganizationId: null,
                OrganizationType: null,
                UserId: null,
                UserName: null,
                Profile: _cabinetProfileResolver.Resolve(null));
        }

        var organizationType = AuthorizationHelper.GetOrganizationType(httpContext);
        return new CurrentTenantContext(
            IsAuthenticated: AuthorizationHelper.IsAuthenticated(httpContext),
            OrganizationId: AuthorizationHelper.GetOrganizationId(httpContext),
            OrganizationType: organizationType,
            UserId: AuthorizationHelper.GetUserId(httpContext),
            UserName: AuthorizationHelper.GetUserName(httpContext),
            Profile: _cabinetProfileResolver.Resolve(organizationType));
    }
}
