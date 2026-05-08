namespace Kahoot.Backend.Application.Authentication;

public interface IPlayerAuthService
{
    LoginResult CreateToken(string nickname, string sessionId);
}
