namespace Hackathon.Domain.Accounts;

public static class UserRoles
{
    public const string Candidate = "Candidate";
    public const string Employer = "Employer";

    public static bool IsDefined(string value) =>
        value is Candidate or Employer;
}
