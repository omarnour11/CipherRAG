using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CipherRAG.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServerMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskAssessments",
                columns: table => new
                {
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatternScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MlModelScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LlmJudgeScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QueryHash = table.Column<string>(type: "VARCHAR(128)", maxLength: 128, nullable: false),
                    CompositeRiskScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessments", x => x.AssessmentId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    RoleCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClinicalClearance = table.Column<int>(type: "int", nullable: true),
                    CanManagePolicies = table.Column<bool>(type: "bit", nullable: false),
                    CanViewAuditDashboard = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "AccessPolicies",
                columns: table => new
                {
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AllowedIpRanges = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowedCountries = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeWindow_StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    TimeWindow_EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    IsActive = table.Column<bool>(type: "BIT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessPolicies", x => x.PolicyId);
                    table.ForeignKey(
                        name: "FK_AccessPolicies_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "VARCHAR(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    DocumentID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    Department = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    SensitivityLevel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DigitalSignature = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TrustScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 1.0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.DocumentID);
                    table.ForeignKey(
                        name: "FK_Documents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RagRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaskedPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaskedFinalResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExecutionTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_RagRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "VARCHAR(256)", maxLength: 256, nullable: false),
                    DevicePlatform = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    ChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VectorPointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClearanceLevel = table.Column<int>(type: "int", nullable: false),
                    PoisonStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.ChunkId);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaskingSessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaskingSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_MaskingSessions_RagRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RagRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaskingSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    LogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaskedPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaskedFinalAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RetrievedChunkIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InjectionDetected = table.Column<bool>(type: "BIT", nullable: false),
                    PoisonDetected = table.Column<bool>(type: "BIT", nullable: false),
                    GroundingScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RowHash = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    PrevRowHash = table.Column<string>(type: "VARCHAR(255)", maxLength: 255, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RiskAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_AuditLogs_MaskingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "MaskingSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_RagRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RagRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaskedTokens",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaceHolder = table.Column<string>(type: "VARCHAR(100)", maxLength: 100, nullable: false),
                    EncryptedRealValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaskedTokens", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_MaskedTokens_MaskingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "MaskingSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessPolicies_RoleId",
                table: "AccessPolicies",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RequestId",
                table: "AuditLogs",
                column: "RequestId",
                unique: true,
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RoleId",
                table: "AuditLogs",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RowHash",
                table: "AuditLogs",
                column: "RowHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SessionId",
                table: "AuditLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TsUtc",
                table: "AuditLogs",
                column: "TsUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaskedTokens_SessionId",
                table: "MaskedTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaskingSessions_RequestId",
                table: "MaskingSessions",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaskingSessions_UserId",
                table: "MaskingSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RagRequests_CreatedAt",
                table: "RagRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RagRequests_UserId",
                table: "RagRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessPolicies");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "MaskedTokens");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RiskAssessments");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "MaskingSessions");

            migrationBuilder.DropTable(
                name: "RagRequests");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
