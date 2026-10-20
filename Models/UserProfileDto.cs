namespace FiveDollarPlan.Models;

public record UserProfileDto(
    Guid Id,
    string Name,
    string? AvatarUrl
);