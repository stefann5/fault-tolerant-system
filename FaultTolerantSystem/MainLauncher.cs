using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace FaultTolerantSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            // Handle command line arguments for direct launching
            if (args.Length > 0)
            {
                if (args[0] == "server")
                {
                    var server = new Server.ServerHost();
                    server.Start();
                    return;
                }
                else if (args[0] == "client" && args.Length >= 3)
                {
                    var client = new Client.ClientApplication(args[1], bool.Parse(args[2]));
                    client.Start();
                    return;
                }
            }

            // If no arguments, show the interactive menu
            ShowInteractiveMenu();
        }

        static void ShowInteractiveMenu()
        {
            Console.WriteLine("================================================");
            Console.WriteLine("     FAULT TOLERANT SYSTEM - MAIN LAUNCHER");
            Console.WriteLine("================================================");
            Console.WriteLine();

            Console.WriteLine("Choose launch mode:");
            Console.WriteLine("1. Launch complete system (Server + 4 Clients)");
            Console.WriteLine("2. Launch Server only");
            Console.WriteLine("3. Launch Client only");
            Console.WriteLine("4. Launch automated demo");
            Console.Write("\nEnter your choice (1-4): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    LaunchCompleteSystem();
                    break;
                case "2":
                    LaunchServerOnly();
                    break;
                case "3":
                    LaunchClientOnly();
                    break;
                case "4":
                    LaunchAutomatedDemo();
                    break;
                default:
                    Console.WriteLine("Invalid choice!");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    break;
            }
        }

        static void LaunchCompleteSystem()
        {
            try
            {
                Console.WriteLine("\nLaunching complete system...\n");

                // Get the executable path
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                // Start server in separate process
                var serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "server",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };
                serverProcess.Start();
                Console.WriteLine("Server started...");

                // Wait for server to initialize
                Thread.Sleep(3000);

                // Start 2 working clients
                for (int i = 1; i <= 2; i++)
                {
                    var clientProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"client CLIENT{i} false",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        }
                    };
                    clientProcess.Start();
                    Console.WriteLine($"Started CLIENT{i} (WORKING)");
                    Thread.Sleep(1000);
                }

                // Start 2 standby clients
                for (int i = 3; i <= 4; i++)
                {
                    var clientProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"client CLIENT{i} true",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        }
                    };
                    clientProcess.Start();
                    Console.WriteLine($"Started CLIENT{i} (STANDBY)");
                    Thread.Sleep(1000);
                }

                Console.WriteLine("\nAll components launched successfully!");
                Console.WriteLine("\nInstructions:");
                Console.WriteLine("- In Server window: Press 'L' to list clients, 'S' to simulate failure");
                Console.WriteLine("- In Client window: Press 'F' to simulate failure, 'S' to send message");
                Console.WriteLine("\nPress any key to exit launcher...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching system: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void LaunchServerOnly()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                var serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "server",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };
                serverProcess.Start();

                Console.WriteLine("\nServer launched in separate window.");
                Console.WriteLine("Press any key to exit launcher...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching server: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void LaunchClientOnly()
        {
            try
            {
                Console.Write("Enter client ID (e.g., CLIENT1): ");
                var clientId = Console.ReadLine();

                Console.Write("Is this a standby client? (y/n): ");
                var isStandby = Console.ReadLine()?.ToLower() == "y";

                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                var clientProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"client {clientId} {isStandby}",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };
                clientProcess.Start();

                Console.WriteLine($"\nClient {clientId} launched in separate window.");
                Console.WriteLine("Press any key to exit launcher...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching client: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void LaunchAutomatedDemo()
        {
            try
            {
                Console.WriteLine("\n=== AUTOMATED DEMO ===");
                Console.WriteLine("This demo will:");
                Console.WriteLine("1. Start a server");
                Console.WriteLine("2. Start 2 working clients and 2 standby clients");
                Console.WriteLine("3. Run for demonstration");
                Console.WriteLine();

                // Start server in a background task
                Task.Run(() =>
                {
                    var server = new Server.ServerHost();
                    server.Start();
                });

                Console.WriteLine("Server started...");
                Thread.Sleep(3000);

                // Start working clients in background tasks
                Task.Run(() =>
                {
                    var client1 = new Client.ClientApplication("DEMO-CLIENT1", false);
                    client1.Start();
                });
                Console.WriteLine("Started DEMO-CLIENT1 (WORKING)");
                Thread.Sleep(1000);

                Task.Run(() =>
                {
                    var client2 = new Client.ClientApplication("DEMO-CLIENT2", false);
                    client2.Start();
                });
                Console.WriteLine("Started DEMO-CLIENT2 (WORKING)");
                Thread.Sleep(1000);

                // Start standby clients in background tasks
                Task.Run(() =>
                {
                    var client3 = new Client.ClientApplication("DEMO-CLIENT3", true);
                    client3.Start();
                });
                Console.WriteLine("Started DEMO-CLIENT3 (STANDBY)");
                Thread.Sleep(1000);

                Task.Run(() =>
                {
                    var client4 = new Client.ClientApplication("DEMO-CLIENT4", true);
                    client4.Start();
                });
                Console.WriteLine("Started DEMO-CLIENT4 (STANDBY)");

                Console.WriteLine("\n=== DEMO RUNNING ===");
                Console.WriteLine("The system is now running in demo mode.");
                Console.WriteLine("You can observe the console output here.");
                Console.WriteLine("\nTo test failover:");
                Console.WriteLine("1. Wait for system to stabilize (10 seconds)");
                Console.WriteLine("2. We'll simulate a failure");
                Console.WriteLine("3. Watch the automatic failover happen");

                Thread.Sleep(10000);

                Console.WriteLine("\n!!! SIMULATING FAILURE OF DEMO-CLIENT1 !!!");
                Console.WriteLine("CLIENT1 will now stop sending heartbeats.");

                // Actually trigger the failure simulation
                Task.Run(() =>
                {
                    try
                    {
                        var binding = new WSDualHttpBinding();
                        binding.Security.Mode = WSDualHttpSecurityMode.None;
                        var endpointAddress = new EndpointAddress("http://localhost:8080/FaultTolerantService");
                        var channelFactory = new ChannelFactory<IFaultTolerantService>(binding, endpointAddress);
                        var serviceProxy = channelFactory.CreateChannel();

                        // Call the simulate failure method
                        serviceProxy.SimulateFailure("DEMO-CLIENT1");
                        Console.WriteLine("Failure simulation triggered on server.");

                        channelFactory.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Note: Manual failure simulation not triggered: {ex.Message}");
                        Console.WriteLine("You can press 'F' in CLIENT1 window or 'S' in server window to simulate failure.");
                    }
                });

                Console.WriteLine("After 30 seconds, the server will detect this and activate a standby client.");
                Console.WriteLine("\nWaiting for failover to occur...");
                Thread.Sleep(35000); // Wait for failover to happen

                Console.WriteLine("\n=== FAILOVER SHOULD HAVE OCCURRED ===");
                Console.WriteLine("Check if a standby client has been activated!");

                Console.WriteLine("\nDemo will continue running. Press any key to stop...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in demo: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}