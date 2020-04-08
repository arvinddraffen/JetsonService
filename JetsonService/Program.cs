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

public class UpdateMessage
{
    public uint CID { get; set; }   // Cluster ID
    public uint NID { get; set; }   // Node ID
    public uint freemem { get; set; }   // MB
    public uint usedmem { get; set; }   // MB
    public String NIP { get; set; }  // IPv4 address
    public float[] cpuutil { get; set; }    // %
    public String OS { get; set; }   // name of operating system
    public TimeSpan utime { get; set; } // uptime of the node
    public int frequency;   //Hz
}

namespace JetsonService
{
    /// <summary>
    /// <see cref="Program"/> is the main entry point into JetsonService.
    /// </summary>
    internal class Program
    {
        static string[] NodeIPs;

        static JetsonModels.Context.ClusterContext database;

        private static readonly object myLock = new object();

        private static void Init()
        {
            string ConfigFile = System.IO.File.ReadAllText(@"JetsonServiceConfig.txt");
            NodeIPs = ConfigFile.Split(new Char[] { '\n' });
        }

        private static void ReceiveMessage(int index)
        {
            int frequency = 1;
            while (true)
            {
                Thread.Sleep(1000 / frequency);

                var client = new RestClient("http://" + NodeIPs[index]);
                var request = new RestRequest("/nodeupdate/", Method.GET);
                request.RequestFormat = DataFormat.Json;
                UpdateMessage myMessage;
                do
                {
                    myMessage = client.Execute<UpdateMessage>(request).Data;
                }
                while (myMessage == null);

                frequency = myMessage.frequency;

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
                        OperatingSystem = myMessage.OS,
                        UpTime = myMessage.utime,
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

            database = new JetsonModels.Context.ClusterContext();

            Task[] myTasks = new Task[NodeIPs.Length];
            for (int i = 0; i < NodeIPs.Length; i++)
            {
                myTasks[i] = Task.Factory.StartNew(o => ReceiveMessage((int)o), i);
            }
            Task.WaitAll(myTasks);
        }

        private static void WriteUtilizationData(uint globalNodeId, UpdateMessage myMessage)
        {
            List<CpuCore> myCores = new List<CpuCore>();

            for (uint j = 0; j < myMessage.cpuutil.Length; j++)
            {
                myCores.Add(new CpuCore() { CoreNumber = j, UtilizationPercentage = myMessage.cpuutil[j] });
            }

            // Add utilization information for the node
            database.UtilizationData.Add(new NodeUtilization()
            {
                Id = myMessage.NID,
                TimeStamp = DateTime.Now,
                Cores = myCores,
                MemoryAvailable = myMessage.freemem,
                MemoryUsed = myMessage.usedmem,
                GlobalNodeId = globalNodeId,
            });
        }
    }
}
