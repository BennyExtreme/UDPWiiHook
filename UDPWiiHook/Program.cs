using System;
using System.Net;
using System.Linq;
using System.Net.Sockets;

namespace UDPWiiHook
{
    internal static class Program
    {
        public static IniFile config;

        public static void Main(string[] args)
        {
            // Initialize the configuration file
            config = new IniFile();

            // Check if config content exists, if not create it
            for (int slotIndex = 0; slotIndex < 4; slotIndex++)
            {
                if (!config.KeyExists("whitelist", "Slot-" + slotIndex))
                {
                    config.Write("port", (4434 + slotIndex).ToString(), "Slot-" + slotIndex);
                    config.Write("broadcastName", "UDPWiiHook@{id}", "Slot-" + slotIndex);
                    config.Write("onlyLocalIps", "0", "Slot-" + slotIndex);
                    config.Write("whitelist", "0", "Slot-" + slotIndex);
                    config.Write("blacklist", "0", "Slot-" + slotIndex);
                    config.Write("commaSeparatedIps", "", "Slot-" + slotIndex);
                }
            }

            // Limit ID to range [0x0000, 0xffff] to make IDs consistent between DSU & UDPWii
            ushort id = (ushort)new Random().Next(0x00000, 0x10000);
            Console.WriteLine("[Program] ID = {0:X4}", id);

            try
            {
                DSU.Server.CreateInstance(id, 26760);
                UDPWii.Server.servers = new UDPWii.Server[]
                {
                    new UDPWii.Server(id, 0, ushort.Parse(Program.config.Read("port", "Slot-" + 0))),
                    new UDPWii.Server(id, 1, ushort.Parse(Program.config.Read("port", "Slot-" + 1))),
                    new UDPWii.Server(id, 2, ushort.Parse(Program.config.Read("port", "Slot-" + 2))),
                    new UDPWii.Server(id, 3, ushort.Parse(Program.config.Read("port", "Slot-" + 3)))
                };

                Console.WriteLine("[Program] Possible local addresses:");
                Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                    .Select(x => x.ToString())
                    .Where(x => !x.StartsWith("169.254."))
                    .ToList()
                    .ForEach(x => Console.WriteLine("\t" + x));

                try
                {
                    string publicIpAddress = new WebClient().DownloadString("https://api.ipify.org");
                    Console.WriteLine("[Program] Possible public address: ");
                    Console.WriteLine("\t" + publicIpAddress);
                    Console.WriteLine("Remember to open UDP slots ports in your router if you want to use clients remotely");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Program] Failed to retrieve public IP address (not connected to internet?):");
                    Console.WriteLine("\t" + ex.Message);
                }
            }
            catch(SocketException e)
            {
                Console.Error.WriteLine("[Program] SocketException (running multiple instances?):");
                Console.Error.WriteLine("\t" + e.Message);
                return;
            }

            DSU.Server.theInstance.Start();
            foreach (UDPWii.Server server in UDPWii.Server.servers)
                server.Start();

            Console.ReadKey();

            foreach (UDPWii.Server server in UDPWii.Server.servers)
                server.Stop();
            DSU.Server.theInstance.Stop();

            Console.ReadKey();
        }
        /*
        // For ICLRRuntimeHost::ExecuteInDefaultAppDomain()
        public static int HostedMain(string args)
        {
            try
            {
                // XXX: A lazy way to process arguments
                Main(new string[] { args });
                return 0;
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("[Program] Exception occured:");
                Console.Error.WriteLine(e.ToString());
                return 1;
            }
        }
        */
    }
}
