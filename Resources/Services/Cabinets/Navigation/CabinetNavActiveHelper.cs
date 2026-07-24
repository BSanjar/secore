namespace WebApplication1.Services.Cabinets.Navigation;

public static class CabinetNavActiveHelper
{
    public static bool IsItemActive(CabinetNavItem item, string? activeController, string? activeAction)
    {
        if (item.Id == "doctors" && IsUsersStaffRoute(activeController))
        {
            return true;
        }

        if (string.IsNullOrEmpty(item.Controller))
        {
            return false;
        }

        if (!string.Equals(item.Controller, activeController, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(item.Action, activeAction, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(item.Controller, "Appointments", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(item.Action, "Index", StringComparison.OrdinalIgnoreCase) &&
            IsCrudSubAction(activeAction))
        {
            return true;
        }

        return false;
    }

    public static bool IsGroupOpen(IReadOnlyCollection<CabinetNavItem> children, string? activeController, string? activeAction)
    {
        return children.Any(child => IsItemActive(child, activeController, activeAction));
    }

    private static bool IsUsersStaffRoute(string? activeController) =>
        string.Equals(activeController, "Users", StringComparison.OrdinalIgnoreCase);

    private static bool IsCrudSubAction(string? activeAction) =>
        activeAction is "Create" or "Edit" or "Details";
}
