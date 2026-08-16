using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFE.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WfeActors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    IntegrationAuthenticateId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeActors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WfeBusinessProcesses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Descriptions = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeBusinessProcesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WfeRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Enable = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WfeSchemes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessProcessId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Scheme = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeSchemes_WfeBusinessProcesses_BusinessProcessId",
                        column: x => x.BusinessProcessId,
                        principalTable: "WfeBusinessProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WfeUserRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeUserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeUserRoles_WfeActors_ActorId",
                        column: x => x.ActorId,
                        principalTable: "WfeActors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WfeUserRoles_WfeRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "WfeRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WfeProcessSchemes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchemeId = table.Column<long>(type: "bigint", nullable: true),
                    Scheme = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefiningParameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsObsolete = table.Column<bool>(type: "bit", nullable: false),
                    RootSchemeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RootSchemeId = table.Column<long>(type: "bigint", nullable: true),
                    AllowedActivities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartingTransition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrackHistory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeProcessSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeProcessSchemes_WfeSchemes_SchemeId",
                        column: x => x.SchemeId,
                        principalTable: "WfeSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WfeProcessInstance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    Activity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PreviousActivity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PreviousState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FaultReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ParentInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    RootInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    ForkTransitionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NextScheduledCheckTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeProcessInstance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeProcessInstance_WfeProcessInstance_ParentInstanceId",
                        column: x => x.ParentInstanceId,
                        principalTable: "WfeProcessInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WfeProcessInstance_WfeProcessSchemes_ProcessSchemeId",
                        column: x => x.ProcessSchemeId,
                        principalTable: "WfeProcessSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WfeProcessInstanceParameters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForRootProcess = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeProcessInstanceParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeProcessInstanceParameters_WfeProcessInstance_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "WfeProcessInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WfeProcessTransitionsHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutorActorId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FromActivity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ToActivity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TransitionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartTransitionTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeProcessTransitionsHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeProcessTransitionsHistory_WfeActors_ExecutorActorId",
                        column: x => x.ExecutorActorId,
                        principalTable: "WfeActors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WfeProcessTransitionsHistory_WfeProcessInstance_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "WfeProcessInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WfeProcessWorkItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    ParentInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    RootInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    StartActivity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ForkTransitionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WfeProcessWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfeProcessWorkItems_WfeProcessInstance_ParentInstanceId",
                        column: x => x.ParentInstanceId,
                        principalTable: "WfeProcessInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WfeBusinessProcesses_Name",
                table: "WfeBusinessProcesses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstance_CorrelationId",
                table: "WfeProcessInstance",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstance_CreationDateTime",
                table: "WfeProcessInstance",
                column: "CreationDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstance_ParentInstanceId",
                table: "WfeProcessInstance",
                column: "ParentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstance_ProcessSchemeId_Status",
                table: "WfeProcessInstance",
                columns: new[] { "ProcessSchemeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstance_RootInstanceId",
                table: "WfeProcessInstance",
                column: "RootInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstance_Status_NextScheduledCheckTime",
                table: "WfeProcessInstance",
                columns: new[] { "Status", "NextScheduledCheckTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessInstanceParameters_ProcessInstanceId_Name",
                table: "WfeProcessInstanceParameters",
                columns: new[] { "ProcessInstanceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessSchemes_RootSchemeId_IsObsolete",
                table: "WfeProcessSchemes",
                columns: new[] { "RootSchemeId", "IsObsolete" });

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessSchemes_SchemeId",
                table: "WfeProcessSchemes",
                column: "SchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessTransitionsHistory_ExecutorActorId",
                table: "WfeProcessTransitionsHistory",
                column: "ExecutorActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessTransitionsHistory_ProcessInstanceId",
                table: "WfeProcessTransitionsHistory",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessTransitionsHistory_StartTransitionTime",
                table: "WfeProcessTransitionsHistory",
                column: "StartTransitionTime");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessWorkItems_ParentInstanceId",
                table: "WfeProcessWorkItems",
                column: "ParentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeProcessWorkItems_Status_CreatedDateTime",
                table: "WfeProcessWorkItems",
                columns: new[] { "Status", "CreatedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WfeRoles_Name",
                table: "WfeRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WfeSchemes_BusinessProcessId",
                table: "WfeSchemes",
                column: "BusinessProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_WfeUserRoles_ActorId_RoleId",
                table: "WfeUserRoles",
                columns: new[] { "ActorId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WfeUserRoles_RoleId",
                table: "WfeUserRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WfeProcessInstanceParameters");

            migrationBuilder.DropTable(
                name: "WfeProcessTransitionsHistory");

            migrationBuilder.DropTable(
                name: "WfeProcessWorkItems");

            migrationBuilder.DropTable(
                name: "WfeUserRoles");

            migrationBuilder.DropTable(
                name: "WfeProcessInstance");

            migrationBuilder.DropTable(
                name: "WfeActors");

            migrationBuilder.DropTable(
                name: "WfeRoles");

            migrationBuilder.DropTable(
                name: "WfeProcessSchemes");

            migrationBuilder.DropTable(
                name: "WfeSchemes");

            migrationBuilder.DropTable(
                name: "WfeBusinessProcesses");
        }
    }
}
