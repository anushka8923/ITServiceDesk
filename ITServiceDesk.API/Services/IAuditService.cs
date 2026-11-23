namespace ITServiceDesk.API.Services;

public interface IAuditService
{
    Task LogActionAsync(
        string userId,
        string action,
        string details);
}