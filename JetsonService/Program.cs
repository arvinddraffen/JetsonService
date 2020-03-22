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
            var x = new System.Diagnostics.Stopwatch();
            x.Start();

            for (uint i = 0; i < 20; i++)
            {
                Build(2, i);
            }

            x.Stop();
            Console.WriteLine($"Completed test build in {x.Elapsed.TotalSeconds} sec");
        }

        private static void Build(uint sampleClusterId, uint sampleNodeId)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ClusterContext>();
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseSqlite("Data Source=data.db");
            var database = new ClusterContext(optionsBuilder.Options);

            /* Sample code for providing an entry into the database */

            // Find cluster with Id of 2. If not exists, create new cluster
            var cluster = database.Clusters
                .Include(c => c.Nodes)
                .FirstOrDefault(c => c.Id == sampleClusterId);

            System.Diagnostics.Debug.WriteLine(cluster == null);

            if (cluster == null)
            {
                cluster = new Cluster();
                cluster.Id = sampleClusterId;
                cluster.Nodes = new List<Node>();
                database.Clusters.Add(cluster);
            }

            //// Find in cluster (cluster Id 2) with Id of 1. If not exists, create new node
            var node = cluster.Nodes.FirstOrDefault(n => n.Id == sampleNodeId);

            if (node == null)
            {
                node = new Node()
                {
                    Id = sampleNodeId,
                    IPAddress = "5.4.3.2",
                };
                cluster.Nodes.Add(node);
            }
            database.SaveChanges();

            // add some data for 7 days
            for (int i = 0; i < 1 * 60 * 60 * 24; i++)
            {
                // Add utilization information for the node (of Id 1)
                database.UtilizationData.Add(new NodeUtilization()
                {
                    GlobalNodeId = node.GlobalId,
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
                database.PowerData.Add(new NodePower() { GlobalNodeId = node.GlobalId, Timestamp = DateTime.Now, Current = (i / 3) % 744, Voltage = (i / 4) % 4, Power = ((i / 3) % 744) * ((i / 4) % 4) });
            }
            // Save changes to the database
            database.SaveChanges();

            database.Dispose();
        }
    }
}
