using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Langoose.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAppFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_flags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_flags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    EnglishText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RussianGlosses = table.Column<List<string>>(type: "text[]", nullable: false),
                    ItemKind = table.Column<string>(type: "text", nullable: false),
                    PartOfSpeech = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedByFlow = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Distractors = table.Column<List<string>>(type: "text[]", nullable: false),
                    AcceptedVariants = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dictionary_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "example_sentences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentenceText = table.Column<string>(type: "text", nullable: false),
                    ClozeText = table.Column<string>(type: "text", nullable: false),
                    TranslationHint = table.Column<string>(type: "text", nullable: false),
                    QualityScore = table.Column<double>(type: "double precision", nullable: false),
                    Origin = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_example_sentences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ImportedRows = table.Column<int>(type: "integer", nullable: false),
                    SkippedRows = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stability = table.Column<double>(type: "double precision", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LapseCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnsweredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAnswer = table.Column<string>(type: "text", nullable: false),
                    NormalizedAnswer = table.Column<string>(type: "text", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    FeedbackCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_flags_ItemId",
                table: "content_flags",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_content_flags_UserId",
                table: "content_flags",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_dictionary_items_EnglishText",
                table: "dictionary_items",
                column: "EnglishText");

            migrationBuilder.CreateIndex(
                name: "IX_dictionary_items_OwnerId",
                table: "dictionary_items",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_example_sentences_ItemId",
                table: "example_sentences",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_import_records_UserId",
                table: "import_records",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_review_states_ItemId",
                table: "review_states",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_review_states_UserId",
                table: "review_states",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_study_events_ItemId",
                table: "study_events",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_study_events_UserId",
                table: "study_events",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_flags");

            migrationBuilder.DropTable(
                name: "dictionary_items");

            migrationBuilder.DropTable(
                name: "example_sentences");

            migrationBuilder.DropTable(
                name: "import_records");

            migrationBuilder.DropTable(
                name: "review_states");

            migrationBuilder.DropTable(
                name: "study_events");
        }
    }
}
