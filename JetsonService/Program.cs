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

            var sampleClusterId = 2U;

            // Find cluster with Id of 2. If not exists, create new cluster
            var cluster = database.Clusters.Find(sampleClusterId);
            System.Diagnostics.Debug.WriteLine(cluster == null);

            if (cluster == null)
            {
                cluster = new Cluster();
                cluster.Id = sampleClusterId;
                cluster.Nodes = new List<Node>();
                database.Clusters.Add(cluster);
            }

            var sampleNodeId = 1U;
            //// Find in cluster (cluster Id 2) with Id of 1. If not exists, create new node
            var node = cluster.Nodes.FirstOrDefault(n => n.Id == sampleNodeId);

            if (node == null)
            {
                node = new Node()
                {
                    Id = sampleNodeId,
                    IPAddress = "5.4.3.2",
                    Power = new List<NodePower>(),
                    Utilization = new List<NodeUtilization>(),
                };
                cluster.Nodes.Add(node);
            }

            // add some data for 7 days
            for (int i = 0; i < 1 * 60 * 60 *24; i++)
            {
                // Add utilization information for the node (of Id 1)
                node.Utilization.Add(new NodeUtilization()
                {
                    MemoryAvailable = 5,
                    MemoryUsed = 100 * 1000,
                    TimeStamp = DateTime.Now,
                    Cores = new List<CpuCore>()
                            {
                                new CpuCore() { CoreNumber = 0, UtilizationPercentage = i / 2 },
                                new CpuCore() { CoreNumber = 1, UtilizationPercentage = i % 7500 },
                            },
                });

                // Add power use information for the node (of Id 1)
                node.Power.Add(new NodePower() { Timestamp = DateTime.Now, Current = (i / 3) % 744, Voltage = (i / 4) % 4, Power = ((i / 3) % 744) * ((i / 4) % 4) });
            }
            // Save changes to the database
            database.SaveChanges();
        }
    }
}
