namespace ReasoningAgents.Core.Models
{
    public sealed record CertificationGoal(
        string CertificationCode,
        int DaysAvailable,
        int DailyMinutes
    );
}
