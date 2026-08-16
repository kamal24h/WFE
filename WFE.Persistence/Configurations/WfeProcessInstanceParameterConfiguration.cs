using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WFE.Models;

namespace WFE.Persistence.Configurations
{
    public class WfeProcessInstanceParameterConfiguration : IEntityTypeConfiguration<WfeProcessInstanceParameter>
    {
        public void Configure(EntityTypeBuilder<WfeProcessInstanceParameter> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name).IsRequired().HasMaxLength(256);

            builder.HasOne(p => p.ProcessInstance)
                .WithMany(i => i.Parameters)
                .HasForeignKey(p => p.ProcessInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // One row per (instance, name) - SetAsync upserts against this, GetAllAsync scans it.
            builder.HasIndex(p => new { p.ProcessInstanceId, p.Name }).IsUnique();
        }
    }
}
