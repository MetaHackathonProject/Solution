namespace Hackathon.Application.Context;

public interface IUserContext
{
    string UserId { get; }

    string Email { get; }

    bool IsAuthenticated { get; }
}
