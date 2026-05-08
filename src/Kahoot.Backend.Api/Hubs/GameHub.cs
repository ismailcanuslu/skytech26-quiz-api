using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kahoot.Backend.Api.Hubs;

[Authorize]
public sealed class GameHub : Hub
{
    public async Task JoinGameGroup(string gamePin)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(gamePin));
    }

    public static string GetGroupName(string gamePin) => $"game-{gamePin}";
}
