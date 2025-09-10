using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Linq;

namespace FaultTolerantSystem.Server
{
    public class ServerHost
    {
        private ServiceHost serviceHost;
        private FaultTolerantService serviceInstance;

        public void Start()
        {
            try
            {
                Console.WriteLine("====================================================");
                Console.WriteLine("         FAULT TOLERANT SYSTEM - SERVER");
                Console.WriteLine("====================================================");
                Console.WriteLine();
                Console.WriteLine("Initializing WCF Service for Fault Tolerance...");

                // Create service instance (singleton)
                serviceInstance = new FaultTolerantService();

                // Create service host with the singleton instance
                serviceHost = new ServiceHost(serviceInstance);

                // Clear any existing endpoints from config
                serviceHost.Description.Endpoints.Clear();

                // Create binding
                var binding = new WSDualHttpBinding();
                binding.Security.Mode = WSDualHttpSecurityMode.None;
                binding.MaxReceivedMessageSize = 2147483647;
                binding.MaxBufferPoolSize = 2147483647;
                binding.ReaderQuotas.MaxArrayLength = 2147483647;
                binding.ReaderQuotas.MaxStringContentLength = 2147483647;

                // Add service endpoint programmatically
                serviceHost.AddServiceEndpoint(
                    typeof(IFaultTolerantService),
                    binding,
                    "http://localhost:8080/FaultTolerantService");

                // Add metadata behavior
                var smb = serviceHost.Description.Behaviors.Find<ServiceMetadataBehavior>();
                if (smb == null)
                {
                    smb = new ServiceMetadataBehavior();
                    smb.HttpGetEnabled = true;
                    smb.HttpGetUrl = new Uri("http://localhost:8080/FaultTolerantService/mex");
                    serviceHost.Description.Behaviors.Add(smb);
                }
                else
                {
                    smb.HttpGetEnabled = true;
                    smb.HttpGetUrl = new Uri("http://localhost:8080/FaultTolerantService/mex");
                }

                // Add debug behavior
                var sdb = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();
                if (sdb == null)
                {
                    sdb = new ServiceDebugBehavior();
                    sdb.IncludeExceptionDetailInFaults = true;
                    serviceHost.Description.Behaviors.Add(sdb);
                }
                else
                {
                    sdb.IncludeExceptionDetailInFaults = true;
                }

                // Open the service host
                serviceHost.Open();

                Console.WriteLine("✓ WCF Service started successfully");
                Console.WriteLine("✓ Database initialized and ready");
                Console.WriteLine("✓ Heartbeat monitoring active (30s timeout)");
                Console.WriteLine("✓ Failover mechanism enabled");
                Console.WriteLine();
                Console.WriteLine($"Service URL: http://localhost:8080/FaultTolerantService");
                Console.WriteLine();
                Console.WriteLine("====================================================");
                Console.WriteLine("              SERVER STATUS: READY");
                Console.WriteLine("====================================================");
                Console.WriteLine();
                Console.WriteLine("Monitoring Configuration:");
                Console.WriteLine("  → Heartbeat Interval: 10 seconds");
                Console.WriteLine("  → Client Timeout: 30 seconds");
                Console.WriteLine("  → Expected Clients: 4 (2 working + 2 standby)");
                Console.WriteLine();
                Console.WriteLine("Commands:");
                Console.WriteLine("  L - List all connected clients");
                Console.WriteLine("  S - Simulate client failure");
                Console.WriteLine("  Q - Quit server");
                Console.WriteLine();
                Console.WriteLine("Waiting for clients to connect...");
                Console.WriteLine();

                // Handle server commands
                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("\n[SERVER] Shutdown requested...");
                        break;
                    }
                    else if (key.Key == ConsoleKey.L)
                    {
                        ShowClientStatus();
                    }
                    else if (key.Key == ConsoleKey.S)
                    {
                        SimulateClientFailure();
                    }
                }

                Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER ERROR] Failed to start service: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[SERVER ERROR] Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine("\nPossible causes:");
                Console.WriteLine("  - Port 8080 is already in use");
                Console.WriteLine("  - LocalDB is not available");
                Console.WriteLine("  - Insufficient permissions");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        private void ShowClientStatus()
        {
            try
            {
                var clients = serviceInstance.GetAllClients();
                var workingClients = clients.Where(c => !c.IsStandby && c.Status == ClientStatus.Working).ToList();
                var standbyClients = clients.Where(c => c.IsStandby && c.Status == ClientStatus.Standby).ToList();
                var deadClients = clients.Where(c => c.Status == ClientStatus.Dead).ToList();

                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("               CLIENT STATUS REPORT");
                Console.WriteLine(new string('=', 50));

                Console.WriteLine($"\nTotal Connected Clients: {clients.Length}");
                Console.WriteLine($"Working Clients: {workingClients.Count}");
                Console.WriteLine($"Standby Clients: {standbyClients.Count}");
                Console.WriteLine($"Dead Clients: {deadClients.Count}");

                if (workingClients.Any())
                {
                    Console.WriteLine("\nWORKING CLIENTS:");
                    foreach (var client in workingClients)
                    {
                        var lastHeartbeat = (DateTime.Now - client.LastHeartbeat).TotalSeconds;
                        Console.WriteLine($"  {client.ClientId} - Last heartbeat: {lastHeartbeat:F0}s ago");
                    }
                }

                if (standbyClients.Any())
                {
                    Console.WriteLine("\nSTANDBY CLIENTS:");
                    foreach (var client in standbyClients)
                    {
                        var lastHeartbeat = (DateTime.Now - client.LastHeartbeat).TotalSeconds;
                        Console.WriteLine($"  {client.ClientId} - Last heartbeat: {lastHeartbeat:F0}s ago");
                    }
                }

                if (deadClients.Any())
                {
                    Console.WriteLine("\nDEAD CLIENTS:");
                    foreach (var client in deadClients)
                    {
                        Console.WriteLine($"  {client.ClientId} - Detected dead");
                    }
                }

                if (!clients.Any())
                {
                    Console.WriteLine("\nNo clients connected");
                }

                Console.WriteLine(new string('=', 50));

                // System health check
                if (workingClients.Count < 2 && standbyClients.Count == 0)
                {
                    Console.WriteLine("\nWARNING: System has insufficient redundancy!");
                }
                else if (workingClients.Count >= 1)
                {
                    Console.WriteLine("\nSystem is operating normally");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[SERVER ERROR] Failed to retrieve client status: {ex.Message}");
            }
        }

        private void SimulateClientFailure()
        {
            try
            {
                var clients = serviceInstance.GetAllClients();

                if (!clients.Any())
                {
                    Console.WriteLine("\n[SERVER] No clients connected to simulate failure.");
                    return;
                }

                var activeClients = clients.Where(c => c.Status != ClientStatus.Dead).ToList();

                if (!activeClients.Any())
                {
                    Console.WriteLine("\n[SERVER] No active clients available for failure simulation.");
                    return;
                }

                Console.WriteLine("\n" + new string('-', 30));
                Console.WriteLine("       FAILURE SIMULATION");
                Console.WriteLine(new string('-', 30));
                Console.WriteLine("\nActive clients:");

                for (int i = 0; i < activeClients.Count; i++)
                {
                    var client = activeClients[i];
                    var type = client.IsStandby ? "STANDBY" : "WORKING";
                    Console.WriteLine($"  {i + 1}. {client.ClientId} ({type})");
                }

                Console.Write($"\nSelect client to fail (1-{activeClients.Count}): ");
                var input = Console.ReadLine();

                if (int.TryParse(input, out int selection) &&
                    selection >= 1 && selection <= activeClients.Count)
                {
                    var selectedClient = activeClients[selection - 1];
                    Console.WriteLine($"\n[SERVER] Simulating failure of {selectedClient.ClientId}...");

                    serviceInstance.SimulateFailure(selectedClient.ClientId);

                    Console.WriteLine($"[SERVER] ✓ {selectedClient.ClientId} marked as failed");

                    if (!selectedClient.IsStandby)
                    {
                        Console.WriteLine("[SERVER] → Failover process initiated");
                        Console.WriteLine("[SERVER] → Searching for standby client to activate...");
                    }
                }
                else
                {
                    Console.WriteLine("\n[SERVER] Invalid selection.");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[SERVER ERROR] Failed to simulate failure: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                Console.WriteLine("\n[SERVER] Shutting down service...");
                if (serviceHost != null && serviceHost.State == CommunicationState.Opened)
                {
                    serviceHost.Close();
                }
                Console.WriteLine("[SERVER] ✓ Service stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER ERROR] Error during shutdown: {ex.Message}");
                serviceHost?.Abort();
            }
        }
    }
}