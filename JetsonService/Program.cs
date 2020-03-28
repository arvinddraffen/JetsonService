using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using JetsonModels;
using JetsonService.Data;
using Microsoft.EntityFrameworkCore;
using RestSharp;

struct UpdateMessage
{
    public uint CID, NID;   // Cluster ID, Node ID
    public uint freemem, usedmem;   // MB
    public String NIP;  // IPv4 address
    public float[] cpu_util;    // %
};

namespace JetsonService
{
    /// <summary>
    /// <see cref="Program"/> is the main entry point into JetsonService.
    /// </summary>
    internal class Program
    {
        static List<string> NodeIPs = new List<string>();

        static ClusterContext database;

        private static readonly object myLock = new object();

        private static void Init()
        {
            string ConfigFile = System.IO.File.ReadAllText(@"JetsonServiceConfig.txt");
            string[] SplitConfigFile = ConfigFile.Split(new Char[] { '\n' });
            NodeIPs.AddRange(SplitConfigFile);
        }

        private static void ReceiveMessage(int index)
        {
            while(true)
            {
                var client = new RestClient("https://" + NodeIPs[index]);
                var request = new RestRequest(Method.GET);
                request.RequestFormat = DataFormat.Json;
                var myMessage = client.Execute<UpdateMessage>(request).Data;    // Execute is synchronous, so it should block until request is received.

                // Find cluster with received Id. If not exists, create new cluster
                var cluster = database.Clusters
                    .Include(c => c.Nodes)
                    .FirstOrDefault(c => c.Id == myMessage.CID);

                if (cluster == null)
                {
                    cluster = new Cluster();
                    cluster.Id = myMessage.CID;
                    cluster.Nodes = new List<Node>();
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
                    };
                    cluster.Nodes.Add(node);
                }

                lock (myLock)
                {
                    database.SaveChanges();
                    WriteUtilizationData(node.GlobalId, myMessage);
                    database.SaveChanges();
                }
            }
        }

        private static void Main(string[] args)
        {
            Init();

            var optionsBuilder = new DbContextOptionsBuilder<ClusterContext>();
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseSqlite("Data Source=data.db");
            database = new ClusterContext(optionsBuilder.Options);

            Task[] myTasks = new Task[NodeIPs.Count];
            for (int i = 0; i < NodeIPs.Count; i++)
            {
                myTasks[i] = Task.Factory.StartNew(() => ReceiveMessage(i));
            }
            Task.WaitAll(myTasks);
        }

        private static void WriteUtilizationData(uint globalNodeId, UpdateMessage myMessage)
        {
            List<CpuCore> myCores = new List<CpuCore>();

            for (uint j = 0; j < myMessage.cpu_util.Length; j++)
            {
                myCores.Add(new CpuCore() { CoreNumber = j, UtilizationPercentage = 100f * myMessage.cpu_util[j] });
            }

            // Add utilization information for the node
            database.UtilizationData.Add(new NodeUtilization()
            {
                GlobalNodeId = globalNodeId,
                MemoryAvailable = myMessage.freemem,
                MemoryUsed = myMessage.usedmem,
                TimeStamp = DateTime.Now,
                Cores = myCores,
            });

            // Add power use information for the node
            int i = (new Random()).Next(1, 10);
            database.PowerData.Add(new NodePower() { GlobalNodeId = globalNodeId, Timestamp = DateTime.Now, Current = ((float)i / (float)3) % 744, Voltage = ((float)i / (float)4) % 4, Power = (((float)i / (float)1000)) * (((float)i / (float)2000)) });
        }
    }
}
