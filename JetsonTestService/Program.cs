using System;
using System.Collections.Generic;
using System.Linq;

using JetsonModels.Context;
using JetsonModels.Database;
using Microsoft.EntityFrameworkCore;

namespace JetsonTestService
{
    /// <summary>
    /// <see cref="Program"/> is the main entry point into JetsonTestService.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            uint sampleClusterId = 2;

            var x = new System.Diagnostics.Stopwatch();
            x.Start();

            ClusterContext database = new ClusterContext();

            /* Sample code for providing an entry into the database */

            // Find cluster with Id of 2. If not exists, create new cluster
            var cluster = database.Clusters
                .Include(c => c.Nodes)
                .FirstOrDefault(c => c.Id == sampleClusterId);

            if (cluster == null)
            {
                cluster = new Cluster
                {
                    Id = sampleClusterId,
                    Nodes = new List<Node>(),
                    RefreshRate = TimeSpan.FromSeconds(5),
                    Type = Cluster.ClusterType.Jetson,
                    ClusterName = "Jetson 2.0",
                };
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
            int dayCounter = 1;
            for (int entry = 0; entry < 1 * 60 * 60 * 24 * 7; entry++)
            {
                foreach (var node in cluster.Nodes)
                {
                    WriteUtilizationData(database, node.GlobalId, entry);
                }

                if (entry % 100000 == 0)
                {
                    Console.WriteLine($"Completed approx. day {dayCounter}");
                    dayCounter++;
                    database.SaveChanges();
                }
            }

            database.SaveChanges();

            x.Stop();
            Console.WriteLine($"Completed database test build in {x.Elapsed.TotalSeconds} sec");
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
                    Current = (i / 3F) % 744,
                    Voltage = (i / 4F) % 4,
                    Power = (i / 1000F) * (i / 2000F),
                });
        }
    }
}
