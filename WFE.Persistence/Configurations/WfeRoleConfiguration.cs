using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WFE.Models;

namespace WFE.Persistence.Configurations
{
    public class WfeRoleConfiguration : IEntityTypeConfiguration<WfeRole>
    {
        public void Configure(EntityTypeBuilder<WfeRole> builder)
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(256);
            builder.HasIndex(r => r.Name).IsUnique();
        }
    }

    public class WfeUserRoleConfiguration : IEntityTypeConfiguration<WfeUserRole>
    {
        public void Configure(EntityTypeBuilder<WfeUserRole> builder)
        {
            builder.HasKey(ur => ur.Id);

            builder.HasOne(ur => ur.Actor)
                .WithMany(a => a.WfeUserRoles)
                .HasForeignKey(ur => ur.ActorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ur => ur.Role)
                .WithMany(r => r.WfeUserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Same actor shouldn't hold the same role twice.
            builder.HasIndex(ur => new { ur.ActorId, ur.RoleId }).IsUnique();
        }
    }
}
