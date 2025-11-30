using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuarryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalYearSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountFiscalYearBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountFiscalYearBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountFiscalYearBalances_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountFiscalYearBalances_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6954));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6969));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6970));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6971));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6972));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6973));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6974));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6975));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6976));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(6977));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(7099));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(7105));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(7107));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(7108));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(7117));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 27, 12, 41, 12, 566, DateTimeKind.Local).AddTicks(7119));

            migrationBuilder.CreateIndex(
                name: "IX_AccountFiscalYearBalances_AccountId_FiscalYearId",
                table: "AccountFiscalYearBalances",
                columns: new[] { "AccountId", "FiscalYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountFiscalYearBalances_FiscalYearId",
                table: "AccountFiscalYearBalances",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_YearCode",
                table: "FiscalYears",
                column: "YearCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountFiscalYearBalances");

            migrationBuilder.DropTable(
                name: "FiscalYears");

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7273));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7290));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7292));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7294));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7295));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7297));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7298));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7299));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7300));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7484));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7491));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7493));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7495));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7497));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7499));
        }
    }
}
