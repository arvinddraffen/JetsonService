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
            uint sampleClusterId = 2;

            var x = new System.Diagnostics.Stopwatch();
            x.Start();

            var optionsBuilder = new DbContextOptionsBuilder<ClusterContext>();
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseSqlite("Data Source=data.db");
            var database = new ClusterContext(optionsBuilder.Options);

            /* Sample code for providing an entry into the database */

            // Find cluster with Id of 2. If not exists, create new cluster
            var cluster = database.Clusters
                .Include(c => c.Nodes)
                .FirstOrDefault(c => c.Id == sampleClusterId);

            if (cluster == null)
            {
                cluster = new Cluster();
                cluster.Id = sampleClusterId;
                cluster.Nodes = new List<Node>();
                cluster.RefreshRate = TimeSpan.FromSeconds(5);
                cluster.clusterType = Cluster.ClusterType.Jetson;
                cluster.ClusterName = "Jetson 2.0";
                database.Clusters.Add(cluster);
            }

            // Make 20 nodes
            for (uint nodeIndex = 0; nodeIndex < 20; nodeIndex++)
            {
                //// Find in cluster (cluster Id 2) with Id of 1. If not exists, create new node
                var node = cluster.Nodes.FirstOrDefault(n => n.Id == nodeIndex);

                if (node == null)
                {
                    node = new Node()
                    {
                        Id = nodeIndex,
                        IPAddress = $"5.4.3.{nodeIndex}",
                    };
                    cluster.Nodes.Add(node);
                }

                database.SaveChanges();
            }

            // add some data for 7 days
            for (int entry = 0; entry < 1 * 60 * 60 * 24 * 7; entry++)
            {
                foreach (var node in cluster.Nodes)
                {
                    WriteUtilizationData(database, node.GlobalId, entry);
                }

                if (entry % 100000 == 0)
                {
                    database.SaveChanges();
                }
            }

            database.SaveChanges();

            x.Stop();
            Console.WriteLine($"Completed test build in {x.Elapsed.TotalSeconds} sec");
        }

        private static void WriteUtilizationData(ClusterContext database, uint globalNodeId, int i)
        {
            var startTime = new DateTime(2020, 3, 27, 00, 00, 00);
            var thisTime = startTime.AddSeconds(i);

            // Add utilization information for the node (of Id 1)
            database.UtilizationData.Add(new NodeUtilization()
            {
                GlobalNodeId = globalNodeId,
                MemoryAvailable = 5,
                MemoryUsed = 100 * 1000,
                TimeStamp = thisTime,
                Cores = new List<CpuCore>()
                        {
                            new CpuCore() { CoreNumber = 0, UtilizationPercentage = i / 2 },
                            new CpuCore() { CoreNumber = 1, UtilizationPercentage = i % 7500 },
                        },
            });

            // Add power use information for the node (of Id 1)
            database.PowerData.Add(new NodePower()
                {
                    GlobalNodeId = globalNodeId,
                    Timestamp = thisTime,
                    Current = ((float)i / (float)3) % 744,
                    Voltage = ((float)i / (float)4) % 4,
                    Power = (((float)i / (float)1000)) * (((float)i / (float)2000)),
                });
        }
    }
}
