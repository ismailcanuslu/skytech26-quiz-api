using System.Security.Cryptography;
using Kahoot.Backend.Application.GameSessions;

namespace Kahoot.Backend.Infrastructure.GameSessions;

internal sealed class GamePinGenerator : IGamePinGenerator
{
    public string GenerateSixDigitPin()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
