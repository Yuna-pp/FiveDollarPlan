using Microsoft.EntityFrameworkCore;
using FiveDollarPlan.Models;

namespace FiveDollarPlan.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}