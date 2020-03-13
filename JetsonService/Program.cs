using System;
using System.Collections.Generic;
using System.Linq;

using JetsonModels;
using JetsonService.Data;
using Microsoft.EntityFrameworkCore;

namespace JetsonService
{
    /// <summary>
    /// <see cref="Program"/> is the main entry point into JetsonService.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ClusterContext>();
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseLazyLoadingProxies();
            optionsBuilder.UseSqlite("Data Source=data.db");
            var database = new ClusterContext(optionsBuilder.Options);

            /* Sample code for providing an entry into the database */

            // Find cluster with Id of 2. If not exists, create new cluster
            var cluster = database.Clusters.Find((uint)2);
            if (cluster == null)
            {
                cluster = new Cluster();
                cluster.Id = 2;
                cluster.Nodes = new List<Node>();
                database.Clusters.Add(cluster);
            }

            // Find in cluster (cluster Id 2) with Id of 1. If not exists, create new node
            var node = cluster.Nodes.FirstOrDefault(n => n.Id == 1);
            if (node == null)
            {
                cluster.Nodes.Add(
                  new Node()
                  {
                      Id = 1,
                      IPAddress = "5.4.3.2",
                      Power = new List<NodePower>(),
                      Utilization = new List<NodeUtilization>(),
                  });
            }

            // Add utilization information for the node (of Id 1)
            node.Utilization.Add(new NodeUtilization()
            {
                MemoryAvailable = 5,
                MemoryUsed = 100 * 1000,
                TimeStamp = DateTime.Now,
                Cores = new List<CpuCore>()
                            {
                                new CpuCore() { CoreNumber = 0, UtilizationPercentage = 1000 / 2 },
                                new CpuCore() { CoreNumber = 1, UtilizationPercentage = 1000 + 1000 },
                            },
            });

            // Add power use information for the node (of Id 1)
            node.Power.Add(new NodePower() { Timestamp = DateTime.Now, Current = 1000, Voltage = 1000 });

            // Save changes to the database
            database.SaveChanges();
        }
    }
}
