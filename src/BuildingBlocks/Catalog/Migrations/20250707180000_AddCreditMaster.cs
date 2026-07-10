using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Catalog.Migrations;

public partial class AddCreditMaster : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[creditMaster]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[creditMaster] (
                    [id] INT IDENTITY(1,1) NOT NULL,
                    [tenantId] UNIQUEIDENTIFIER NOT NULL,
                    [allocationMonth] INT NOT NULL,
                    [allocationYear] INT NOT NULL,
                    [creditType] NVARCHAR(100) NULL,
                    [initialCredit] INT NOT NULL DEFAULT 0,
                    [balanceCredit] INT NOT NULL DEFAULT 0,
                    [remarks] NVARCHAR(500) NULL,
                    [createdAt] DATETIME2 NULL,
                    [modifiedAt] DATETIME2 NULL,
                    [createdBy] NVARCHAR(50) NULL,
                    [modifiedBy] NVARCHAR(50) NULL,
                    [isDeleted] BIT NOT NULL DEFAULT 0,
                    [ValidFrom] DATETIME2 NULL,
                    [ValidTo] DATETIME2 NULL,
                    [parentAllocationId] INT NULL,
                    [subscriptionType] NVARCHAR(100) NULL,
                    [validFromDate] DATETIME2 NULL,
                    [validToDate] DATETIME2 NULL,
                    [isCarryForward] BIT NULL,
                    [priority] INT NULL,
                    [status] NVARCHAR(50) NULL,
                    [carryForwardCredit] INT NULL,
                    [extraConsumedCredit] INT NULL,
                    [topUpBalanceCredit] INT NULL,
                    [overallConsumedCredit] INT NULL,
                    CONSTRAINT [PK_creditMaster] PRIMARY KEY CLUSTERED ([id] ASC)
                );
                CREATE INDEX [IX_creditMaster_Tenant_Period]
                    ON [dbo].[creditMaster] ([tenantId], [allocationYear], [allocationMonth], [creditType]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[creditMaster];");
    }
}
