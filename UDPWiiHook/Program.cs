using System;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;

namespace UDPWiiHook
{
    internal static class Program
    {
        public const string programName = "UDPWiiHook";
        public const string programVersion = "1.0.7";
        public static IniFile config;

        public static void Main(string[] args)
        {
            // Initialize the configuration file
            config = new IniFile();

            // Check if config content exists and if the content is valid, if not fix it
            if (!config.KeyExists("port", programName) || !int.TryParse(config.Read("port", programName), out _)) config.Write("port", "26760", programName);  // "out _" -> "out int value"
            if (!config.KeyExists("checkUpdates", programName) || !int.TryParse(config.Read("checkUpdates", programName), out _)) config.Write("checkUpdates", "1", programName);
            if (!config.KeyExists("popupWindow", programName) || !int.TryParse(config.Read("popupWindow", programName), out _)) config.Write("popupWindow", "1", programName);

            for (int slotIndex = 0; slotIndex < 4; slotIndex++)
            {
                if (!config.KeyExists("port", "Slot-" + slotIndex) || !int.TryParse(config.Read("port", "Slot-" + slotIndex), out _)) config.Write("port", (4434 + slotIndex).ToString(), "Slot-" + slotIndex);
                if (!config.KeyExists("broadcastName", "Slot-" + slotIndex)) config.Write("broadcastName", "UDPWiiHook@{id}", "Slot-" + slotIndex);
                if (!config.KeyExists("onlyLocalIps", "Slot-" + slotIndex) || !int.TryParse(config.Read("onlyLocalIps", "Slot-" + slotIndex), out _)) config.Write("onlyLocalIps", "0", "Slot-" + slotIndex);
                if (!config.KeyExists("whitelist", "Slot-" + slotIndex) || !int.TryParse(config.Read("whitelist", "Slot-" + slotIndex), out _)) config.Write("whitelist", "0", "Slot-" + slotIndex);
                if (!config.KeyExists("blacklist", "Slot-" + slotIndex) || !int.TryParse(config.Read("blacklist", "Slot-" + slotIndex), out _)) config.Write("blacklist", "0", "Slot-" + slotIndex);
                if (!config.KeyExists("commaSeparatedIps", "Slot-" + slotIndex)) config.Write("commaSeparatedIps", "", "Slot-" + slotIndex);
            }

            // Check for updates
            if (int.Parse(config.Read("checkUpdates", programName)) == 1)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent: UDPWiiHook");
                        string getLatest = client.DownloadString("https://api.github.com/repos/BennyExtreme/UDPWiiHook/releases/latest");
                        string latestVersion = getLatest.Split(new String[] { "\"tag_name\":\"" }, StringSplitOptions.None)[1].Split(new String[] { "\"," }, StringSplitOptions.None)[0];

                        if (latestVersion != programVersion)
                        {
                            Console.WriteLine("--- --- ---\nA new version of the program is available for download!\n\nLatest Version: {0}\nCurrent Version: {1}\n\nYou can disable version check in the .ini config file.\n--- --- ---", latestVersion, programVersion);
                            if (int.Parse(config.Read("popupWindow", programName)) == 1)
                            {
                                DialogResult result = MessageBox.Show(String.Format("A new version of the program is available for download,\nclick \"OK\" to open the download website.\n\nLatest Version: {0}\nCurrent Version: {1}\n\nYou can disable version check in the .ini config file.", latestVersion, programVersion), "New version available", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

                                if (result == DialogResult.OK)
                                {
                                    Process.Start("https://github.com/BennyExtreme/UDPWiiHook/releases/latest");
                                    Console.WriteLine("Download website has been opened");
                                }
                                else
                                {
                                    Console.WriteLine("Update warning dismissed");
                                }
                                Console.WriteLine("--- --- ---");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("[Program] Version check failed (ratelimit or not connected to internet?)");
                }
            }

            // Limit ID to range [0x0000, 0xffff] to make IDs consistent between DSU & UDPWii
            ushort id = (ushort)new Random().Next(0x00000, 0x10000);
            Console.WriteLine("[Program] ID = {0:X4}", id);

            try
            {
                DSU.Server.CreateInstance(id, ushort.Parse(config.Read("port", programName)));
                UDPWii.Server.servers = new UDPWii.Server[]
                {
                    new UDPWii.Server(id, 0, ushort.Parse(config.Read("port", "Slot-" + 0))),
                    new UDPWii.Server(id, 1, ushort.Parse(config.Read("port", "Slot-" + 1))),
                    new UDPWii.Server(id, 2, ushort.Parse(config.Read("port", "Slot-" + 2))),
                    new UDPWii.Server(id, 3, ushort.Parse(config.Read("port", "Slot-" + 3)))
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
                catch (Exception)
                {
                    Console.WriteLine("[Program] Failed to retrieve public IP address (not connected to internet?)");
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
