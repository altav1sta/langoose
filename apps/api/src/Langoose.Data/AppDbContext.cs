using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();
    public DbSet<ExampleSentence> ExampleSentences => Set<ExampleSentence>();
    public DbSet<ReviewState> ReviewStates => Set<ReviewState>();
    public DbSet<StudyEvent> StudyEvents => Set<StudyEvent>();
    public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();
    public DbSet<ContentFlag> ContentFlags => Set<ContentFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
