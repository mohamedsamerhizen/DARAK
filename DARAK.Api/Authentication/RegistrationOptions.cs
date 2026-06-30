namespace DARAK.Api.Authentication;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public bool EnablePublicRegistration { get; set; }

    public bool AutoConfirmRegisteredUsers { get; set; }
}
