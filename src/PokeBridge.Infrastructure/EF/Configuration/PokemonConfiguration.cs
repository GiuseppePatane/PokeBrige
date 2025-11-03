using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokeBridge.Core.Pokemon;

namespace PokeBridge.Infrastructure.EF.Configuration;

public class PokemonConfiguration : IEntityTypeConfiguration<PokemonRace>
{
    public void Configure(EntityTypeBuilder<PokemonRace> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(1000);
        builder.Property(p => p.Habitat).HasMaxLength(100);
        builder.Property(p => p.IsLegendary).IsRequired();

        builder.Property(x=> x.Translations).HasColumnType("jsonb");
   
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

