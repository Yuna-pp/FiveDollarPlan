using System.ComponentModel.DataAnnotations;

namespace FiveDollarPlan.Models;

public class JoinPlanViewModel
{
    [Required(ErrorMessage = "Enter an invitation code.")]
    [Display(Name = "Invitation code")]
    public string InviteCode { get; set; } = string.Empty;
}
