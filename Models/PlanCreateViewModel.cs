using System.ComponentModel.DataAnnotations;

namespace FiveDollarPlan.Models;

public class PlanCreateViewModel
{
    [Required(ErrorMessage = "Enter a plan name.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "5", ErrorMessage = "Amount must be between NZ$0 and NZ$5.")]
    public decimal Amount { get; set; }

    [Required]
    public PlanMode? Mode { get; set; }

    [Required(ErrorMessage = "Choose a start date and time.")]
    [Display(Name = "Plan starts")]
    public DateTime? StartAtLocal { get; set; }

    [Required(ErrorMessage = "Choose an end date and time.")]
    [Display(Name = "Plan ends")]
    public DateTime? EndAtLocal { get; set; }

    [Display(Name = "Allowed trigger period starts (optional)")]
    public DateTime? TriggerStartAtLocal { get; set; }

    [Display(Name = "Allowed trigger period ends (optional)")]
    public DateTime? TriggerEndAtLocal { get; set; }
}
