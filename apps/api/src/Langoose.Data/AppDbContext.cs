using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DictionaryEntry> DictionaryEntries => Set<DictionaryEntry>();
    public DbSet<Sense> Senses => Set<Sense>();
    public DbSet<SenseTranslation> SenseTranslations => Set<SenseTranslation>();
    public DbSet<EntryContext> EntryContexts => Set<EntryContext>();
    public DbSet<UserEntry> UserEntries => Set<UserEntry>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<StudyEvent> StudyEvents => Set<StudyEvent>();
    public DbSet<ContentFlag> ContentFlags => Set<ContentFlag>();
    public DbSet<UserImport> UserImports => Set<UserImport>();
    public DbSet<ImportEntry> ImportEntries => Set<ImportEntry>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
