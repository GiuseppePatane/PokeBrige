using Microsoft.EntityFrameworkCore;
using PokeBridge.Core.Pokemon;

namespace PokeBridge.Infrastructure.EF;

public class PokeBridgeDbContext : DbContext
{
    
   public DbSet<PokemonRace> PokemonRaces { get; set; }
    
    public PokeBridgeDbContext(DbContextOptions<PokeBridgeDbContext> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder) 
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PokeBridgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}