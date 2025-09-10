using System;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace FaultTolerantSystem.Server
{
    public class ServerHost
    {
        private ServiceHost serviceHost;

        public void Start()
        {
            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("   FAULT TOLERANT SYSTEM - SERVER");
                Console.WriteLine("========================================");
                Console.WriteLine();

                // Create service instance (singleton)
                var serviceInstance = new FaultTolerantService();

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

                Console.WriteLine("[SERVER] Service is running at http://localhost:8080/FaultTolerantService");
                Console.WriteLine("[SERVER] Press 'Q' to quit");
                Console.WriteLine("[SERVER] Press 'L' to list all clients");
                Console.WriteLine("[SERVER] Press 'S' to simulate failure");
                Console.WriteLine();

                // Handle server commands
                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Q)
                    {
                        break;
                    }
                    else if (key.Key == ConsoleKey.L)
                    {
                        ListClients();
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
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private void ListClients()
        {
            try
            {
                var service = (FaultTolerantService)serviceHost.SingletonInstance;
                var clients = service.GetAllClients();

                Console.WriteLine("\n========== CONNECTED CLIENTS ==========");
                if (clients.Length == 0)
                {
                    Console.WriteLine("No clients connected.");
                }
                else
                {
                    foreach (var client in clients)
                    {
                        Console.WriteLine($"ID: {client.ClientId}");
                        Console.WriteLine($"  Status: {client.Status}");
                        Console.WriteLine($"  Type: {(client.IsStandby ? "STANDBY" : "WORKING")}");
                        Console.WriteLine($"  Last Heartbeat: {client.LastHeartbeat:HH:mm:ss}");
                        Console.WriteLine($"  Registered: {client.RegisteredAt:HH:mm:ss}");
                        Console.WriteLine();
                    }
                }
                Console.WriteLine("=======================================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER ERROR] Failed to list clients: {ex.Message}");
            }
        }

        private void SimulateClientFailure()
        {
            try
            {
                var service = (FaultTolerantService)serviceHost.SingletonInstance;
                var clients = service.GetAllClients();

                if (clients.Length == 0)
                {
                    Console.WriteLine("[SERVER] No clients connected to simulate failure.");
                    return;
                }

                Console.WriteLine("\nConnected clients:");
                foreach (var client in clients)
                {
                    Console.WriteLine($"  - {client.ClientId} ({client.Status})");
                }

                Console.Write("\nEnter client ID to simulate failure: ");
                var clientId = Console.ReadLine();

                service.SimulateFailure(clientId);

                Console.WriteLine($"[SERVER] Simulated failure for client {clientId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER ERROR] Failed to simulate failure: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                Console.WriteLine("[SERVER] Shutting down...");
                if (serviceHost != null && serviceHost.State == CommunicationState.Opened)
                {
                    serviceHost.Close();
                }
                Console.WriteLine("[SERVER] Service stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER ERROR] Error during shutdown: {ex.Message}");
                serviceHost?.Abort();
            }
        }
    }
}