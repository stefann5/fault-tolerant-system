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
            LaunchCompleteSystem();
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

        
    }
}