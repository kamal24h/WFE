using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WFE.Models;

namespace WFE.Persistence.Configurations
{
    public class WfeSchemeConfiguration : IEntityTypeConfiguration<WfeScheme>
    {
        public void Configure(EntityTypeBuilder<WfeScheme> builder)
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Name).HasMaxLength(256);
            builder.Property(s => s.Scheme).IsRequired(); // the designer XML - unbounded text

            builder.HasOne(s => s.BusinessProcess)
                .WithMany(p => p.WfeSchemes)
                .HasForeignKey(s => s.BusinessProcessId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class WfeProcessSchemeConfiguration : IEntityTypeConfiguration<WfeProcessScheme>
    {
        public void Configure(EntityTypeBuilder<WfeProcessScheme> builder)
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Scheme).IsRequired();

            builder.HasOne(s => s.SourceScheme)
                .WithMany()
                .HasForeignKey(s => s.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);

            // "Give me the current published scheme for business process X" is the hot lookup
            // path when your ingestion service starts a new instance - index for it.
            builder.HasIndex(s => new { s.RootSchemeId, s.IsObsolete });
        }
    }

    public class WfeBusinessProcessConfiguration : IEntityTypeConfiguration<WfeBusinessProcess>
    {
        public void Configure(EntityTypeBuilder<WfeBusinessProcess> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(256);

            builder.HasIndex(p => p.Name).IsUnique();
        }
    }
}
