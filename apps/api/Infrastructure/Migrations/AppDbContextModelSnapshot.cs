using Langoose.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Langoose.Api.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
internal sealed class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Langoose.Api.Models.ContentFlag", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("Details")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<Guid>("ItemId")
                .HasColumnType("uuid");

            entity.Property<string>("Reason")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<Guid>("UserId")
                .HasColumnType("uuid");

            entity.HasKey("Id");

            entity.HasIndex("ItemId");

            entity.HasIndex("UserId");

            entity.ToTable("content_flags");
        });

        modelBuilder.Entity("Langoose.Api.Models.DictionaryItem", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<string[]>("AcceptedVariants")
                .IsRequired()
                .HasColumnType("text[]");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("CreatedByFlow")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            entity.Property<string>("Difficulty")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            entity.Property<string[]>("Distractors")
                .IsRequired()
                .HasColumnType("text[]");

            entity.Property<string>("EnglishText")
                .IsRequired()
                .HasMaxLength(300)
                .HasColumnType("character varying(300)");

            entity.Property<string>("ItemKind")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<string>("Notes")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<Guid?>("OwnerId")
                .HasColumnType("uuid");

            entity.Property<string>("PartOfSpeech")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            entity.Property<string[]>("RussianGlosses")
                .IsRequired()
                .HasColumnType("text[]");

            entity.Property<string>("SourceType")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<string>("Status")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<string[]>("Tags")
                .IsRequired()
                .HasColumnType("text[]");

            entity.HasKey("Id");

            entity.HasIndex("EnglishText");

            entity.HasIndex("OwnerId");

            entity.ToTable("dictionary_items");
        });

        modelBuilder.Entity("Langoose.Api.Models.ExampleSentence", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<string>("ClozeText")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<Guid>("ItemId")
                .HasColumnType("uuid");

            entity.Property<string>("Origin")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<double>("QualityScore")
                .HasColumnType("double precision");

            entity.Property<string>("SentenceText")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<string>("TranslationHint")
                .IsRequired()
                .HasColumnType("text");

            entity.HasKey("Id");

            entity.HasIndex("ItemId");

            entity.ToTable("example_sentences");
        });

        modelBuilder.Entity("Langoose.Api.Models.ImportRecord", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("FileName")
                .IsRequired()
                .HasMaxLength(260)
                .HasColumnType("character varying(260)");

            entity.Property<int>("ImportedRows")
                .HasColumnType("integer");

            entity.Property<int>("SkippedRows")
                .HasColumnType("integer");

            entity.Property<int>("TotalRows")
                .HasColumnType("integer");

            entity.Property<Guid>("UserId")
                .HasColumnType("uuid");

            entity.HasKey("Id");

            entity.HasIndex("UserId");

            entity.ToTable("import_records");
        });

        modelBuilder.Entity("Langoose.Api.Models.ReviewState", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("DueAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<Guid>("ItemId")
                .HasColumnType("uuid");

            entity.Property<int>("LapseCount")
                .HasColumnType("integer");

            entity.Property<DateTimeOffset?>("LastSeenAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<double>("Stability")
                .HasColumnType("double precision");

            entity.Property<int>("SuccessCount")
                .HasColumnType("integer");

            entity.Property<Guid>("UserId")
                .HasColumnType("uuid");

            entity.HasKey("Id");

            entity.HasIndex("ItemId");

            entity.HasIndex("UserId");

            entity.ToTable("review_states");
        });

        modelBuilder.Entity("Langoose.Api.Models.SessionToken", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("Token")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.Property<Guid>("UserId")
                .HasColumnType("uuid");

            entity.HasKey("Id");

            entity.HasIndex("Token")
                .IsUnique();

            entity.HasIndex("UserId");

            entity.ToTable("session_tokens");
        });

        modelBuilder.Entity("Langoose.Api.Models.StudyEvent", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("AnsweredAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("FeedbackCode")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<Guid>("ItemId")
                .HasColumnType("uuid");

            entity.Property<string>("NormalizedAnswer")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<string>("SubmittedAnswer")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<Guid>("UserId")
                .HasColumnType("uuid");

            entity.Property<string>("Verdict")
                .IsRequired()
                .HasColumnType("text");

            entity.HasKey("Id");

            entity.HasIndex("ItemId");

            entity.HasIndex("UserId");

            entity.ToTable("study_events");
        });

        modelBuilder.Entity("Langoose.Api.Models.User", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(320)
                .HasColumnType("character varying(320)");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.Property<string>("Provider")
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            entity.Property<string>("ProviderUserId")
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.HasKey("Id");

            entity.ToTable("users");
        });
    }
}
