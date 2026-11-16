namespace MEMS.Shared.Identity;

public enum AdministratorType
{
    None = 0,
    SuperAdmin = 1,
    MunicipalityAdministrator = 2
}

public static class AdminRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string MunicipalityAdministrator = "Municipality Administrator";
}
