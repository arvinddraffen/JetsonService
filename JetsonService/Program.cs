using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using JetsonModels;
using JetsonModels.Database;
using Microsoft.EntityFrameworkCore;
using RestSharp;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using Nancy;
using Nancy.Hosting.Self;
using Nancy.Testing;
using Nancy.Extensions;

public class UpdateMessage
{
    public uint CID { get; set; }   // Cluster ID
    public uint NID { get; set; }   // Node ID
    public uint freemem { get; set; }   // MB
    public uint usedmem { get; set; }   // MB
    public String NIP { get; set; }  // IPv4 address
    public float[] cpuutil { get; set; }    // %
    public String OS { get; set; }   // name of operating system
    public long utime { get; set; } // uptime of the node
    public int frequency { get; set; }   //Hz
}

namespace JetsonService
{
    class NodeModule : NancyModule
    {
        public NodeModule()
        {
            Put("/nodeupdate", args => {
                UpdateMessage myMessage = JsonConvert.DeserializeObject<UpdateMessage>(this.Request.Body.AsString());
                Program.ReceiveMessage(myMessage);
                return "Success";
            });
        }
    }


    /// <summary>
    /// <see cref="Program"/> is the main entry point into JetsonService.
    /// </summary>
    internal class Program
    {
        public static void ReceiveMessage(UpdateMessage myMessage)
        {
            var optionsBuilder = new DbContextOptionsBuilder<JetsonModels.Context.ClusterContext>();
            optionsBuilder.UseSqlite("Data Source=/var/lib/jetson/data.db");
            //optionsBuilder.UseSqlite("Data Source=data.db");

            var options = optionsBuilder.Options;

            JetsonModels.Context.ClusterContext database = new JetsonModels.Context.ClusterContext(options);

            // Find cluster with received Id. If not exists, create new cluster
            var cluster = database.Clusters
                .Include(c => c.Nodes)
                .FirstOrDefault(c => c.Id == myMessage.CID);

            if (cluster == null)
            {
                cluster = new Cluster();
                cluster.Id = myMessage.CID;
                cluster.Nodes = new List<Node>();
                cluster.RefreshRate = TimeSpan.FromMilliseconds(1000 / myMessage.frequency);
                cluster.Type = Cluster.ClusterType.Jetson;
                cluster.ClusterName = "Jetson 2.0";
                database.Clusters.Add(cluster);

            }



            //// Find in cluster with given node Id. If not exists, create new node
            var node = cluster.Nodes.FirstOrDefault(n => n.Id == myMessage.NID);

            if (node == null)
            {
                node = new Node()
                {
                    Id = myMessage.NID,
                    IPAddress = myMessage.NIP,
                    OperatingSystem = myMessage.OS,
                    UpTime = new TimeSpan(myMessage.utime),
                };
                cluster.Nodes.Add(node);
                database.SaveChanges();

            }

            Console.WriteLine("Cluster {0} has {1} nodes.", cluster.Id, cluster.Nodes.Count);

            List<CpuCore> myCores = new List<CpuCore>();

            for (uint j = 0; j < myMessage.cpuutil.Length; j++)
            {
                myCores.Add(new CpuCore() { CoreNumber = j, UtilizationPercentage = myMessage.cpuutil[j] });
            }

            // Add utilization information for the node
            database.UtilizationData.Add(new NodeUtilization()
            {
                TimeStamp = DateTime.Now,
                Cores = myCores,
                MemoryAvailable = myMessage.freemem,
                MemoryUsed = myMessage.usedmem,
                GlobalNodeId = node.GlobalId,
            });

            database.SaveChanges();

            int i = new Random().Next(1, 10);

            database.PowerData.Add(new NodePower()
            {
                GlobalNodeId = node.GlobalId,
                Timestamp = DateTime.Now,
                Current = (i / 3F) % 744,
                Voltage = (i / 4F) % 4,
                Power = (i / 1000F) * (i / 2000F),
            });

            Console.WriteLine("Cluster ID = {0}", myMessage.CID);
            Console.WriteLine("Node ID = {0}", myMessage.NID);
            Console.WriteLine("GlobalID = {0}", node.GlobalId);
            Console.WriteLine(database.SaveChanges());
        }

        private static void Main(string[] args)
        {
            HostConfiguration hostConfigs = new HostConfiguration();
            hostConfigs.UrlReservations.CreateAutomatically = true;
            var bootstrapper = new ConfigurableBootstrapper(with =>
            {
                with.Module<NodeModule>();
            });
            using (var nancyHost = new NancyHost(bootstrapper, hostConfigs, new Uri("http://localhost:9200/")))
            {
                nancyHost.Start();

                var optionsBuilder = new DbContextOptionsBuilder<JetsonModels.Context.ClusterContext>();
                optionsBuilder.UseSqlite("Data Source=/var/lib/jetson/data.db");
                //optionsBuilder.UseSqlite("Data Source=data.db");

                var options = optionsBuilder.Options;

                JetsonModels.Context.ClusterContext database = new JetsonModels.Context.ClusterContext(options);

                while (true)
                {
                    DateTime now = DateTime.Now;
                    var weekOldNodeUtils = database.UtilizationData.Where(x => now.Ticks - x.TimeStamp.Ticks >= new TimeSpan(7, 0, 0, 0).Ticks);
                    var weekOldPowerData = database.PowerData.Where(x => now.Ticks - x.Timestamp.Ticks >= new TimeSpan(7, 0, 0, 0).Ticks);
                    foreach (NodeUtilization weekOldNodeUtil in weekOldNodeUtils)
                    {
                        database.UtilizationData.Remove(weekOldNodeUtil);
                    }
                    foreach (NodePower weekOldPowerDatum in weekOldPowerData)
                    {
                        database.PowerData.Remove(weekOldPowerDatum);
                    }
                    if (weekOldNodeUtils.ToArray<NodeUtilization>().Count<NodeUtilization>() != 0 || weekOldPowerData.ToArray<NodePower>().Count<NodePower>() != 0)
                        database.SaveChanges();
                }
            }

        }
    }
}
