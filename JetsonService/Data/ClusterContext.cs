using System;
using System.Collections.Generic;
using System.Text;

using JetsonModels;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace JetsonService.Data
{
    /// <summary>
    /// <see cref="ClusterContext"/> provides an interface to the SQLite database with EntityFramework.
    /// </summary>
    public class ClusterContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterContext"/> class.
        /// </summary>
        /// <remarks>
        /// Will create the SQLite database if not already created.
        /// </remarks>
        /// <param name="options"></param>
        public ClusterContext(DbContextOptions<ClusterContext> options)
            : base(options)
        {
            this.Database.EnsureCreated();
        }

        /// <summary>
        /// Gets or sets the list of clusters (<see cref="Cluster"/>) which constitute the system.
        /// </summary>
        public DbSet<Cluster> Clusters { get; set; }

        public DbSet<NodePower> PowerData { get; set; }

        public DbSet<NodeUtilization> UtilizationData { get; set; }

        /// <inheritdoc/>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodeUtilization>()
                .Property(x => x.Cores)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<ICollection<CpuCore>>(v));

            // Index on NodeUtilization to speedup lookups
            modelBuilder.Entity<NodeUtilization>()
                .HasIndex(x => x.GlobalNodeId);

            // Index on NodePower to speedup lookups
            modelBuilder.Entity<NodePower>()
                .HasIndex(x => x.GlobalNodeId);
        }
    }
}
