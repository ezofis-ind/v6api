using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSApp.Users.Infrastructure.Migrations
{
    public partial class AddExtendedUserProfile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "users",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "users",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileId",
                schema: "users",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNo",
                schema: "users",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryEmail",
                schema: "users",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "users",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "users",
                table: "Users",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                schema: "users",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                schema: "users",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                schema: "users",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                schema: "users",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthStrategy",
                schema: "users",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginType",
                schema: "users",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginName",
                schema: "users",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "users",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                schema: "users",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                schema: "users",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorAuthentication",
                schema: "users",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PasswordAge",
                schema: "users",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubjectId",
                schema: "users",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MicrosoftOid",
                schema: "users",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarPath",
                schema: "users",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCardPath",
                schema: "users",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignaturePath",
                schema: "users",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UiPreference",
                schema: "users",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                schema: "users",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "users",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "users",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "users",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FirstName", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "LastName", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "ProfileId", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "PhoneNo", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "SecondaryEmail", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "Language", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "CountryCode", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "Department", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "JobTitle", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "ManagerId", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "UserType", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "AuthStrategy", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "LoginType", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "LoginName", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordHash", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "PinHash", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "DeviceId", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "TwoFactorAuthentication", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordAge", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "GoogleSubjectId", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "MicrosoftOid", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "AvatarPath", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "IdCardPath", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "SignaturePath", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "UiPreference", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "ModifiedAtUtc", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "CreatedBy", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "ModifiedBy", schema: "users", table: "Users");
            migrationBuilder.DropColumn(name: "IsDeleted", schema: "users", table: "Users");
        }
    }
}
