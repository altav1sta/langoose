using Langoose.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Langoose.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();
    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();
    public DbSet<ExampleSentence> ExampleSentences => Set<ExampleSentence>();
    public DbSet<ReviewState> ReviewStates => Set<ReviewState>();
    public DbSet<StudyEvent> StudyEvents => Set<StudyEvent>();
    public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();
    public DbSet<ContentFlag> ContentFlags => Set<ContentFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder.Entity<User>());
        ConfigureSessionToken(modelBuilder.Entity<SessionToken>());
        ConfigureDictionaryItem(modelBuilder.Entity<DictionaryItem>());
        ConfigureExampleSentence(modelBuilder.Entity<ExampleSentence>());
        ConfigureReviewState(modelBuilder.Entity<ReviewState>());
        ConfigureStudyEvent(modelBuilder.Entity<StudyEvent>());
        ConfigureImportRecord(modelBuilder.Entity<ImportRecord>());
        ConfigureContentFlag(modelBuilder.Entity<ContentFlag>());
    }

    private static void ConfigureUser(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");
        entity.HasKey(user => user.Id);
        entity.Property(user => user.Email).HasMaxLength(320);
        entity.Property(user => user.Name).HasMaxLength(200);
        entity.Property(user => user.Provider).HasMaxLength(100);
        entity.Property(user => user.ProviderUserId).HasMaxLength(200);
    }

    private static void ConfigureSessionToken(EntityTypeBuilder<SessionToken> entity)
    {
        entity.ToTable("session_tokens");
        entity.HasKey(token => token.Id);
        entity.Property(token => token.Token).HasMaxLength(200);
        entity.HasIndex(token => token.Token).IsUnique();
        entity.HasIndex(token => token.UserId);
    }

    private static void ConfigureDictionaryItem(EntityTypeBuilder<DictionaryItem> entity)
    {
        entity.ToTable("dictionary_items");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.SourceType).HasConversion<string>();
        entity.Property(item => item.ItemKind).HasConversion<string>();
        entity.Property(item => item.Status).HasConversion<string>();
        entity.Property(item => item.EnglishText).HasMaxLength(300);
        entity.Property(item => item.PartOfSpeech).HasMaxLength(100);
        entity.Property(item => item.Difficulty).HasMaxLength(20);
        entity.Property(item => item.CreatedByFlow).HasMaxLength(100);
        entity.Property(item => item.RussianGlosses).HasColumnType("text[]");
        entity.Property(item => item.Tags).HasColumnType("text[]");
        entity.Property(item => item.Distractors).HasColumnType("text[]");
        entity.Property(item => item.AcceptedVariants).HasColumnType("text[]");
        entity.HasIndex(item => item.OwnerId);
        entity.HasIndex(item => item.EnglishText);
    }

    private static void ConfigureExampleSentence(EntityTypeBuilder<ExampleSentence> entity)
    {
        entity.ToTable("example_sentences");
        entity.HasKey(sentence => sentence.Id);
        entity.Property(sentence => sentence.Origin).HasConversion<string>();
        entity.HasIndex(sentence => sentence.ItemId);
    }

    private static void ConfigureReviewState(EntityTypeBuilder<ReviewState> entity)
    {
        entity.ToTable("review_states");
        entity.HasKey(state => state.Id);
        entity.HasIndex(state => state.UserId);
        entity.HasIndex(state => state.ItemId);
    }

    private static void ConfigureStudyEvent(EntityTypeBuilder<StudyEvent> entity)
    {
        entity.ToTable("study_events");
        entity.HasKey(studyEvent => studyEvent.Id);
        entity.Property(studyEvent => studyEvent.Verdict).HasConversion<string>();
        entity.Property(studyEvent => studyEvent.FeedbackCode).HasConversion<string>();
        entity.HasIndex(studyEvent => studyEvent.UserId);
        entity.HasIndex(studyEvent => studyEvent.ItemId);
    }

    private static void ConfigureImportRecord(EntityTypeBuilder<ImportRecord> entity)
    {
        entity.ToTable("import_records");
        entity.HasKey(record => record.Id);
        entity.Property(record => record.FileName).HasMaxLength(260);
        entity.HasIndex(record => record.UserId);
    }

    private static void ConfigureContentFlag(EntityTypeBuilder<ContentFlag> entity)
    {
        entity.ToTable("content_flags");
        entity.HasKey(flag => flag.Id);
        entity.HasIndex(flag => flag.UserId);
        entity.HasIndex(flag => flag.ItemId);
    }
}
