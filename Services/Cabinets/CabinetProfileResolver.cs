namespace WebApplication1.Services.Cabinets;

public interface ICabinetProfileResolver
{
    CabinetProfile Resolve(string? organizationType);
}

public sealed class CabinetProfileResolver : ICabinetProfileResolver
{
    private static readonly CabinetProfile StandartProfile = new(
        Key: "standart",
        OrganizationType: "standart",
        LayoutPath: "~/Views/Shared/CabinetLayouts/_StandartCabinetLayout.cshtml",
        Features: new[]
        {
            CabinetFeatures.Dashboard,
            CabinetFeatures.Payments,
            CabinetFeatures.Invoices,
            CabinetFeatures.Settings,
            CabinetFeatures.Notifications
        });

    private static readonly CabinetProfile SimpleProfile = new(
        Key: "simple",
        OrganizationType: "simple",
        LayoutPath: "~/Views/Shared/CabinetLayouts/_SimpleCabinetLayout.cshtml",
        Features: new[]
        {
            CabinetFeatures.Dashboard,
            CabinetFeatures.Payments,
            CabinetFeatures.Settings,
            CabinetFeatures.Notifications
        });

    private static readonly CabinetProfile DetsadProfile = new(
        Key: "detsad",
        OrganizationType: "detsad",
        LayoutPath: "~/Views/Shared/CabinetLayouts/_DetsadCabinetLayout.cshtml",
        Features: new[]
        {
            CabinetFeatures.Dashboard,
            CabinetFeatures.Clients,
            CabinetFeatures.Payments,
            CabinetFeatures.Invoices,
            CabinetFeatures.OrgGroups,
            CabinetFeatures.OrgServices,
            CabinetFeatures.Profile,
            CabinetFeatures.Settings,
            CabinetFeatures.Notifications
        });

    private static readonly CabinetProfile MedclinicProfile = new(
        Key: "medclinic",
        OrganizationType: "medclinic",
        LayoutPath: "~/Views/Shared/CabinetLayouts/_MedclinicCabinetLayout.cshtml",
        Features: new[]
        {
            CabinetFeatures.Dashboard,
            CabinetFeatures.Profile,
            CabinetFeatures.Settings,
            CabinetFeatures.Notifications,
            CabinetFeatures.Payments,
            CabinetFeatures.Patients,
            CabinetFeatures.Appointments,
            CabinetFeatures.Doctors,
            CabinetFeatures.MedicalStructure,
            CabinetFeatures.OrgServices
        });

    public CabinetProfile Resolve(string? organizationType)
    {
        return Normalize(organizationType) switch
        {
            "detsad" => DetsadProfile,
            "simple" => SimpleProfile,
            "medclinic" => MedclinicProfile,
            "school" => StandartProfile with
            {
                Key = "school",
                OrganizationType = "school"
            },
            _ => StandartProfile
        };
    }

    private static string Normalize(string? organizationType) =>
        (organizationType ?? string.Empty).Trim().ToLowerInvariant();
}
