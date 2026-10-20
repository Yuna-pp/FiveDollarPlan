using Microsoft.EntityFrameworkCore;
using FiveDollarPlan.Models;

namespace FiveDollarPlan.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // =========================
    // Database Tables
    // =========================

    public DbSet<User> Users { get; set; }
    public DbSet<Plan> Plans { get; set; }
    public DbSet<PlanMember> PlanMembers { get; set; }
    public DbSet<PenaltyEvent> PenaltyEvents { get; set; }
    public DbSet<PaymentRecord> PaymentRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================
        // User
        // =========================

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // =========================
        // Plan
        // =========================

        // Each plan has one creator
        modelBuilder.Entity<Plan>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Invitation code must be unique
        modelBuilder.Entity<Plan>()
            .HasIndex(p => p.InviteCode)
            .IsUnique();

        // =========================
        // PlanMember
        // =========================

        // Each member belongs to a plan
        modelBuilder.Entity<PlanMember>()
            .HasOne<Plan>()
            .WithMany()
            .HasForeignKey(pm => pm.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Each member refers to a user
        modelBuilder.Entity<PlanMember>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A user can only join the same plan once
        modelBuilder.Entity<PlanMember>()
            .HasIndex(pm => new { pm.PlanId, pm.UserId })
            .IsUnique();

        // =========================
        // PenaltyEvent
        // =========================

        // Each penalty event belongs to a plan
        modelBuilder.Entity<PenaltyEvent>()
            .HasOne<Plan>()
            .WithMany()
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Each penalty event has a creator
        modelBuilder.Entity<PenaltyEvent>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // =========================
        // PaymentRecord
        // =========================

        // Each payment belongs to a penalty event
        modelBuilder.Entity<PaymentRecord>()
            .HasOne<PenaltyEvent>()
            .WithMany()
            .HasForeignKey(p => p.PenaltyEventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Payment payer
        modelBuilder.Entity<PaymentRecord>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.PayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Payment recipient
        modelBuilder.Entity<PaymentRecord>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}