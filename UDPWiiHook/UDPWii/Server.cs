using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace UDPWiiHook.UDPWii
{
    // This struct is a class since it needs to be reference-able
    internal class Client
    {
        public byte slot;
        public byte touchId;
        public bool touching;
        public bool gyroSeen;
        public DateTime lastSeen;
        public uint packetNumber;
        public string attachedTo;
    }

    internal class Server
    {
        public static UDPWii.Server[] servers;

        // ---

        private readonly UdpClient udp;
        private readonly IPEndPoint endPointLocal;
        private readonly UDPWii.Broadcaster broadcaster;
        private CancellationTokenSource tokenSource;
        private Task task;

        public readonly Client client;

        private void TaskMain(CancellationToken token)
        {
            broadcaster.Start();

            try
            {
                Task<UdpReceiveResult> taskRecv;
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    //Util.TimingStart();
                    taskRecv = udp.ReceiveAsync();
                    taskRecv.Wait(token);
                    //Console.WriteLine(Util.TimingEnd());

                    // Check if client is attached to an address, attach it if not
                    if (client.attachedTo == "")
                    {
                        client.attachedTo = taskRecv.Result.RemoteEndPoint.ToString();
                        Console.WriteLine("[UDPWii.Server@{0}] Now attached to {1}", client.slot, client.attachedTo);
                    }
                    // If the client is already attached then check if the address of the packet is the same as the attached one, otherwise skip the packet
                    else if (client.attachedTo != taskRecv.Result.RemoteEndPoint.ToString())
                    {
                        //Console.WriteLine("[UDPWii.Server@{0}] Packet from non attached client {1} detected, skipping", client.slot, taskRecv.Result.RemoteEndPoint.ToString());
                        continue;
                    }

                    // Mark this server's client as "alive"
                    client.lastSeen = DateTime.Now;

                    // Box data for DSU.Sender
                    UDPWii.DataDSU packet = new UDPWii.DataDSU(taskRecv.Result.Buffer, client);
                    if (packet.flags == UDPWii.Data.Flags.None)
                    {
                        Console.WriteLine("[UDPWii.Server@{0}] Dropping invalid packet from {1}", client.slot, taskRecv.Result.RemoteEndPoint.ToString());
                    }
                    else
                    {
                        //Util.TimingStart();
                        DSU.Server.theInstance.SendDataPacket(packet);
                        //Console.Error.WriteLine(Util.TimingEnd());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ;
            }

            broadcaster.Stop();
        }

        public Server(ushort id, byte slot, ushort port)
        {
            this.endPointLocal = new IPEndPoint(IPAddress.Any, port);
            this.udp = new UdpClient(endPointLocal);

            this.client = new Client();
            this.client.slot = slot;
            this.client.lastSeen = DateTime.MinValue;
            this.client.attachedTo = "";

            this.broadcaster = new Broadcaster(String.Format("UDPWiiHook@{0:X4}", id), id, slot, port);

            Console.WriteLine("[UDPWii.Server@{0}] Initialized at port {1}", slot, port);
        }

        ~Server()
        {
            Stop();
            udp.Close();
        }

        public void Start()
        {
            Stop();

            tokenSource = new CancellationTokenSource();
            task = Task.Run(() => TaskMain(tokenSource.Token), tokenSource.Token);
        }

        public void Stop()
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
                task.Wait();
            }

            task = null;
            tokenSource = null;
        }
    }
}
