namespace Kahoot.Backend.Api.Services;

public interface IGameRoundScheduler
{
    void Schedule(string gamePin, int questionIndex, int timeLimitSeconds);
}
