using Microsoft.AspNetCore.Identity;
using static ChipRoyale.Web.Localization.Loc;

namespace ChipRoyale.Web.Localization;

/// <summary>Identity error messages (registration, password) in German or English.</summary>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError E(string code, string text) => new() { Code = code, Description = text };

    public override IdentityError DefaultError() => E(nameof(DefaultError), L("IdentityError.DefaultError"));
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), L("IdentityError.DuplicateEmail", email));
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), L("IdentityError.DuplicateEmail", userName));
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), L("IdentityError.InvalidEmail", email ?? ""));
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), L("IdentityError.InvalidEmail", userName ?? ""));
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), L("IdentityError.PasswordMismatch"));
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), L("IdentityError.PasswordTooShort", length));
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), L("IdentityError.PasswordRequiresDigit"));
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), L("IdentityError.PasswordRequiresLower"));
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), L("IdentityError.PasswordRequiresUpper"));
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), L("IdentityError.PasswordRequiresNonAlphanumeric"));
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), L("IdentityError.PasswordRequiresUniqueChars", uniqueChars));
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), L("IdentityError.InvalidToken"));
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), L("IdentityError.DefaultError"));
}

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
}
