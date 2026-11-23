using static ChipRoyale.Client.Localization.Loc;

namespace ChipRoyale.Client.Localization;

/// <summary>Validation texts for form models (DataAnnotations read these static properties).</summary>
public static class ValidationTexts
{
    public static string Required => L("Validation.Required");
    public static string EmailAddress => L("Validation.EmailAddress");
    public static string PasswordLength => L("Validation.PasswordLength");
    public static string PasswordCompare => L("Validation.PasswordCompare");
    public static string EmailName => L("Account.Email");
    public static string PasswordName => L("Account.Password");
    public static string ConfirmPasswordName => L("Account.ConfirmPassword");
    public static string RememberMeName => L("Account.RememberMe");

    /// <summary>Turns an error code from the server's account API into a readable sentence.</summary>
    public static string IdentityError(string code, string email) => code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => L("IdentityError.DuplicateEmail", email),
        "InvalidEmail" or "InvalidUserName" => L("IdentityError.InvalidEmail", email),
        "PasswordTooShort" => L("IdentityError.PasswordTooShort", 6),
        "PasswordRequiresDigit" => L("IdentityError.PasswordRequiresDigit"),
        "PasswordRequiresLower" => L("IdentityError.PasswordRequiresLower"),
        "PasswordRequiresUpper" => L("IdentityError.PasswordRequiresUpper"),
        "PasswordRequiresNonAlphanumeric" => L("IdentityError.PasswordRequiresNonAlphanumeric"),
        "PasswordMismatch" => L("IdentityError.PasswordMismatch"),
        _ => L("IdentityError.DefaultError"),
    };
}
