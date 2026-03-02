namespace ReasoningAgents.Domain.Models
{
    public sealed record CertificationGoal(
        string CertificationCode,
        int DaysAvailable,
        int DailyMinutes
    );
}
