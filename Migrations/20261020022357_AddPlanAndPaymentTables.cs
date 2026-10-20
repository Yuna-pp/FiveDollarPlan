using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiveDollarPlan.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanAndPaymentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TriggerStartAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggerEndAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InviteCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plans_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PenaltyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PenaltyEvents_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PenaltyEvents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanMembers_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PenaltyEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_PenaltyEvents_PenaltyEventId",
                        column: x => x.PenaltyEventId,
                        principalTable: "PenaltyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_Users_PayerId",
                        column: x => x.PayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_PayerId",
                table: "PaymentRecords",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_PenaltyEventId",
                table: "PaymentRecords",
                column: "PenaltyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_RecipientId",
                table: "PaymentRecords",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyEvents_CreatedByUserId",
                table: "PenaltyEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyEvents_PlanId",
                table: "PenaltyEvents",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanMembers_PlanId_UserId",
                table: "PlanMembers",
                columns: new[] { "PlanId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanMembers_UserId",
                table: "PlanMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_CreatorId",
                table: "Plans",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_InviteCode",
                table: "Plans",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentRecords");

            migrationBuilder.DropTable(
                name: "PlanMembers");

            migrationBuilder.DropTable(
                name: "PenaltyEvents");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
