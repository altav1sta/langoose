using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Langoose.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialNewDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dictionary_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsBaseForm = table.Column<bool>(type: "boolean", nullable: false),
                    BaseEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrammarLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dictionary_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dictionary_entries_dictionary_entries_BaseEntryId",
                        column: x => x.BaseEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    PendingCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_flags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DictionaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_flags_dictionary_entries_DictionaryEntryId",
                        column: x => x.DictionaryEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_contexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DictionaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Cloze = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entry_contexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entry_contexts_dictionary_entries_DictionaryEntryId",
                        column: x => x.DictionaryEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_translations",
                columns: table => new
                {
                    SourceEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entry_translations", x => new { x.SourceEntryId, x.TargetEntryId });
                    table.ForeignKey(
                        name: "FK_entry_translations_dictionary_entries_SourceEntryId",
                        column: x => x.SourceEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entry_translations_dictionary_entries_TargetEntryId",
                        column: x => x.TargetEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_dictionary_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DictionaryEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserInputTerm = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UserInputTranslation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EnrichmentStatus = table.Column<string>(type: "text", nullable: false),
                    EnrichmentAttempts = table.Column<int>(type: "integer", nullable: false),
                    EnrichmentNotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_dictionary_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_dictionary_entries_dictionary_entries_DictionaryEntryId",
                        column: x => x.DictionaryEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DictionaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Stability = table.Column<double>(type: "double precision", nullable: true),
                    Difficulty = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_progress_dictionary_entries_DictionaryEntryId",
                        column: x => x.DictionaryEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "context_translations",
                columns: table => new
                {
                    SourceContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_translations", x => new { x.SourceContextId, x.TargetContextId });
                    table.ForeignKey(
                        name: "FK_context_translations_entry_contexts_SourceContextId",
                        column: x => x.SourceContextId,
                        principalTable: "entry_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_context_translations_entry_contexts_TargetContextId",
                        column: x => x.TargetContextId,
                        principalTable: "entry_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DictionaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserInput = table.Column<string>(type: "text", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    FeedbackCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_study_events_dictionary_entries_DictionaryEntryId",
                        column: x => x.DictionaryEntryId,
                        principalTable: "dictionary_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_events_entry_contexts_EntryContextId",
                        column: x => x.EntryContextId,
                        principalTable: "entry_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_flags_DictionaryEntryId",
                table: "content_flags",
                column: "DictionaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_context_translations_TargetContextId",
                table: "context_translations",
                column: "TargetContextId");

            migrationBuilder.CreateIndex(
                name: "IX_dictionary_entries_BaseEntryId",
                table: "dictionary_entries",
                column: "BaseEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_dictionary_entries_Language_Text",
                table: "dictionary_entries",
                columns: new[] { "Language", "Text" });

            migrationBuilder.CreateIndex(
                name: "IX_entry_contexts_DictionaryEntryId",
                table: "entry_contexts",
                column: "DictionaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_entry_translations_TargetEntryId",
                table: "entry_translations",
                column: "TargetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_import_records_UserId",
                table: "import_records",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_study_events_DictionaryEntryId",
                table: "study_events",
                column: "DictionaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_study_events_EntryContextId",
                table: "study_events",
                column: "EntryContextId");

            migrationBuilder.CreateIndex(
                name: "IX_study_events_UserId",
                table: "study_events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_DictionaryEntryId",
                table: "user_dictionary_entries",
                column: "DictionaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_EnrichmentStatus",
                table: "user_dictionary_entries",
                column: "EnrichmentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_UserId",
                table: "user_dictionary_entries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_UserId_DictionaryEntryId",
                table: "user_dictionary_entries",
                columns: new[] { "UserId", "DictionaryEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_progress_DictionaryEntryId",
                table: "user_progress",
                column: "DictionaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_user_progress_UserId_DictionaryEntryId",
                table: "user_progress",
                columns: new[] { "UserId", "DictionaryEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_progress_UserId_DueAtUtc",
                table: "user_progress",
                columns: new[] { "UserId", "DueAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_flags");

            migrationBuilder.DropTable(
                name: "context_translations");

            migrationBuilder.DropTable(
                name: "entry_translations");

            migrationBuilder.DropTable(
                name: "import_records");

            migrationBuilder.DropTable(
                name: "study_events");

            migrationBuilder.DropTable(
                name: "user_dictionary_entries");

            migrationBuilder.DropTable(
                name: "user_progress");

            migrationBuilder.DropTable(
                name: "entry_contexts");

            migrationBuilder.DropTable(
                name: "dictionary_entries");
        }
    }
}
