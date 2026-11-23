namespace ITServiceDesk.API.DTOs;

public class CategoryTicketStatsDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int TicketCount { get; set; }
}