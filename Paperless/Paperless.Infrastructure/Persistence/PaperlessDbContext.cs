using Microsoft.EntityFrameworkCore;
using Paperless.Domain.Entities;
using Paperless.Shared.Utils;
using System.Collections.Generic;

namespace Paperless.Infrastructure.Persistence
{
    public class PaperlessDbContext : DbContext
    {
        public PaperlessDbContext(DbContextOptions<PaperlessDbContext> options)
            : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<DocumentDailyAccess> DocumentDailyAccesses => Set<DocumentDailyAccess>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Tag>(b =>
            {
                b.HasKey(t => t.Name);
                b.Property(t => t.Name).HasMaxLength(ValidationRules.TagMaxLength).IsRequired();
            });

            builder.Entity<Document>(b =>
            {
                b.HasMany(d => d.Tags)
                 .WithMany(t => t.Documents)  // if Tag has a back-reference to Documents
                 //.WithMany()    // no back-reference on Tag
                 .UsingEntity<Dictionary<string, object>>(
                     "DocumentTag",
                     j => j.HasOne<Tag>().WithMany().HasForeignKey("TagName").HasPrincipalKey(nameof(Tag.Name)),
                     j => j.HasOne<Document>().WithMany().HasForeignKey("DocumentId"),
                     j =>
                     {
                         j.HasKey("DocumentId", "TagName");
                         j.Property<string>("TagName").HasMaxLength(ValidationRules.TagMaxLength);
                     });
            });

            builder.Entity<DocumentDailyAccess>(entity =>
            {
                entity.HasKey(e => new { e.DocumentId, e.Date, e.AccessType });

                entity.Property(e => e.AccessType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Count)
                    .IsRequired();

                entity.Property(e => e.Date)
                    .HasColumnType("date");

                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => new { e.DocumentId, e.Date });
            });
        }

    }
}
