using WebApplication1.Models.DBModels;

namespace WebApplication1.Helpers;

public static class LandingPageResolver
{
    public const string Cabinet = "cabinet";
    public const string Appointments = "appointments";

    private static readonly HashSet<string> AllowedStartPages = new(StringComparer.OrdinalIgnoreCase)
    {
        Cabinet,
        Appointments
    };

    public static bool IsValidStartPage(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && AllowedStartPages.Contains(value);
    }

    public static string NormalizeStartPage(string? value)
    {
        if (!IsValidStartPage(value))
            return Cabinet;

        return value!.Trim().ToLowerInvariant();
    }

    public static string ResolveRoute(HttpContext httpContext, User? user)
    {
        var organizationType = AuthorizationHelper.GetOrganizationType(httpContext);
        var preferred = NormalizeStartPage(user?.StartPage);
        var canOpenAppointments = string.Equals(organizationType, "medclinic", StringComparison.OrdinalIgnoreCase)
                                  && (PermissionHelper.HasPermission(httpContext, "appointments.registry.view")
                                      || PermissionHelper.HasPermission(httpContext, "appointments.doctor.view")
                                      || PermissionHelper.HasPermission(httpContext, "appointments.view"));
        var canOpenCabinet = PermissionHelper.HasPermission(httpContext, "dashboard.view");
        if (preferred == Appointments && canOpenAppointments)
            return Appointments;

        if (preferred == Cabinet && canOpenCabinet)
            return Cabinet;

        if (canOpenCabinet)
            return Cabinet;

        if (canOpenAppointments)
            return Appointments;

        return Cabinet;
    }
}
