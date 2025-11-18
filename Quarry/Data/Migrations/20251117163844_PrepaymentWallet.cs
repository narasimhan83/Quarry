using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuarryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrepaymentWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extend invoices for prepayment tracking
            migrationBuilder.AddColumn<bool>(
                name: "IsFullyPrepaid",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PrepaymentApplied",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Customer prepayment wallet table
            migrationBuilder.CreateTable(
                name: "CustomerPrepayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PrepaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrepaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UsedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPrepayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPrepayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Applications of prepayments to invoices
            migrationBuilder.CreateTable(
                name: "PrepaymentApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerPrepaymentId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrepaymentApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrepaymentApplications_CustomerPrepayments_CustomerPrepaymentId",
                        column: x => x.CustomerPrepaymentId,
                        principalTable: "CustomerPrepayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrepaymentApplications_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Seed customer prepayment liability account and keep existing seed updates
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

            migrationBuilder.InsertData(
                table: "ChartOfAccounts",
                columns: new[] { "Id", "AccountCode", "AccountName", "AccountType", "CreatedAt", "CurrentBalance", "IsActive", "OpeningBalance", "SubType" },
                values: new object[] { 10, "2103", "Customer Prepayments", "Liability", new DateTime(2025, 11, 17, 19, 38, 43, 688, DateTimeKind.Local).AddTicks(7300), 0m, true, 0m, "Current" });

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

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPrepayments_CustomerId",
                table: "CustomerPrepayments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PrepaymentApplications_CustomerPrepaymentId",
                table: "PrepaymentApplications",
                column: "CustomerPrepaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PrepaymentApplications_InvoiceId",
                table: "PrepaymentApplications",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrepaymentApplications");

            migrationBuilder.DropTable(
                name: "CustomerPrepayments");

            migrationBuilder.DeleteData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DropColumn(
                name: "IsFullyPrepaid",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PrepaymentApplied",
                table: "Invoices");

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 479, DateTimeKind.Local).AddTicks(9941));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 479, DateTimeKind.Local).AddTicks(9961));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 479, DateTimeKind.Local).AddTicks(9962));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 479, DateTimeKind.Local).AddTicks(9963));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 479, DateTimeKind.Local).AddTicks(9964));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 479, DateTimeKind.Local).AddTicks(9965));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(3));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(4));

            migrationBuilder.UpdateData(
                table: "ChartOfAccounts",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(5));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(217));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(224));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(226));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(229));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(231));

            migrationBuilder.UpdateData(
                table: "Materials",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 19, 24, 19, 480, DateTimeKind.Local).AddTicks(232));
        }
    }
}
