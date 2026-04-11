using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DictionaryEntry> DictionaryEntries => Set<DictionaryEntry>();
    public DbSet<EntryTranslation> EntryTranslations => Set<EntryTranslation>();
    public DbSet<EntryContext> EntryContexts => Set<EntryContext>();
    public DbSet<ContextTranslation> ContextTranslations => Set<ContextTranslation>();
    public DbSet<UserDictionaryEntry> UserDictionaryEntries => Set<UserDictionaryEntry>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<StudyEvent> StudyEvents => Set<StudyEvent>();
    public DbSet<ContentFlag> ContentFlags => Set<ContentFlag>();
    public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
