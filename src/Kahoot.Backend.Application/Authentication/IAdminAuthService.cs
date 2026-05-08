namespace Kahoot.Backend.Application.Authentication;

public interface IAdminAuthService
{
    Task<LoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
