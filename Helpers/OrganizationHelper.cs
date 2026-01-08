using Microsoft.AspNetCore.Http;

namespace WebApplication1.Helpers
{
    public static class OrganizationHelper
    {
        public static string? GetCurrentOrganizationId(HttpContext httpContext)
        {
            // TODO: Реализовать получение ID организации из сессии или токена
            // Пока используем заглушку - можно получить из сессии или из claims
            return httpContext.Session.GetString("OrganizationId");
        }

        public static string? GetCurrentOrganizationType(HttpContext httpContext)
        {
            // TODO: Реализовать получение типа организации из сессии или токена
            return httpContext.Session.GetString("OrganizationType");
        }
    }
}

