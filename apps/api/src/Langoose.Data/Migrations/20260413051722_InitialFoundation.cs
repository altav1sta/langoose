using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Langoose.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dictionary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    base_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    part_of_speech = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    grammar_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_dictionary_entries_dictionary_entries_base_entry_id",
                        column: x => x.base_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    pending_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dictionary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_flags", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_flags_dictionary_entries_dictionary_entry_id",
                        column: x => x.dictionary_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_entries_translations",
                columns: table => new
                {
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_entries_translations", x => new { x.source_id, x.target_id });
                    table.ForeignKey(
                        name: "fk_dictionary_entries_translations_dictionary_entries_source_id",
                        column: x => x.source_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dictionary_entries_translations_dictionary_entries_target_id",
                        column: x => x.target_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_contexts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dictionary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    cloze = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_contexts", x => x.id);
                    table.ForeignKey(
                        name: "fk_entry_contexts_dictionary_entries_dictionary_entry_id",
                        column: x => x.dictionary_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_dictionary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_input_term = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    user_input_translation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    part_of_speech = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enrichment_status = table.Column<string>(type: "text", nullable: false),
                    enrichment_attempts = table.Column<int>(type: "integer", nullable: false),
                    enrichment_not_before = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_dictionary_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_dictionary_entries_dictionary_entries_source_entry_id",
                        column: x => x.source_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_dictionary_entries_dictionary_entries_target_entry_id",
                        column: x => x.target_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dictionary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    due_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stability = table.Column<double>(type: "double precision", nullable: true),
                    difficulty = table.Column<double>(type: "double precision", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_progress_dictionary_entries_dictionary_entry_id",
                        column: x => x.dictionary_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_contexts_translations",
                columns: table => new
                {
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_contexts_translations", x => new { x.source_id, x.target_id });
                    table.ForeignKey(
                        name: "fk_entry_contexts_translations_entry_contexts_source_id",
                        column: x => x.source_id,
                        principalTable: "entry_contexts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_entry_contexts_translations_entry_contexts_target_id",
                        column: x => x.target_id,
                        principalTable: "entry_contexts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dictionary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_context_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_input = table.Column<string>(type: "text", nullable: false),
                    verdict = table.Column<string>(type: "text", nullable: false),
                    feedback_code = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_study_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_events_dictionary_entries_dictionary_entry_id",
                        column: x => x.dictionary_entry_id,
                        principalTable: "dictionary_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_study_events_entry_contexts_entry_context_id",
                        column: x => x.entry_context_id,
                        principalTable: "entry_contexts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_flags_dictionary_entry_id",
                table: "content_flags",
                column: "dictionary_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_entries_base_entry_id",
                table: "dictionary_entries",
                column: "base_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_entries_language_text_part_of_speech",
                table: "dictionary_entries",
                columns: new[] { "language", "text", "part_of_speech" });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_entries_translations_target_id",
                table: "dictionary_entries_translations",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "ix_entry_contexts_dictionary_entry_id",
                table: "entry_contexts",
                column: "dictionary_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_entry_contexts_translations_target_id",
                table: "entry_contexts_translations",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_records_user_id",
                table: "import_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_events_dictionary_entry_id",
                table: "study_events",
                column: "dictionary_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_events_entry_context_id",
                table: "study_events",
                column: "entry_context_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_events_user_id_created_at_utc",
                table: "study_events",
                columns: new[] { "user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_user_dictionary_entries_enrichment_status_created_at_utc",
                table: "user_dictionary_entries",
                columns: new[] { "enrichment_status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_user_dictionary_entries_source_entry_id",
                table: "user_dictionary_entries",
                column: "source_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_dictionary_entries_target_entry_id",
                table: "user_dictionary_entries",
                column: "target_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_dictionary_entries_user_id",
                table: "user_dictionary_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_progress_dictionary_entry_id",
                table: "user_progress",
                column: "dictionary_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_progress_user_id_dictionary_entry_id",
                table: "user_progress",
                columns: new[] { "user_id", "dictionary_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_progress_user_id_due_at_utc",
                table: "user_progress",
                columns: new[] { "user_id", "due_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_flags");

            migrationBuilder.DropTable(
                name: "dictionary_entries_translations");

            migrationBuilder.DropTable(
                name: "entry_contexts_translations");

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
