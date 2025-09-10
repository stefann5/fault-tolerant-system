using System;
using System.ServiceModel;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

namespace FaultTolerantSystem.Client
{
    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public class ClientCallback : IClientCallback
    {
        private readonly ClientApplication client;

        public ClientCallback(ClientApplication client)
        {
            this.client = client;
        }

        public void StartWorking()
        {
            Console.WriteLine($"\n[SERVER SIGNAL] Start working command received!");
            client.StartWorking();
        }

        public void StopWorking()
        {
            Console.WriteLine($"\n[SERVER SIGNAL] Stop working command received!");
            client.StopWorking();
        }

        public void ReceiveMessage(byte[] encryptedData, string senderId)
        {
            try
            {
                var decryptedMessage = client.cryptoManager.Decrypt(encryptedData);
                Console.WriteLine($"\n[ENCRYPTED MESSAGE] From {senderId}: {decryptedMessage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[MESSAGE ERROR] Failed to decrypt message from {senderId}: {ex.Message}");
            }
        }
    }

    public class ClientApplication
    {
        public string ClientId { get; private set; }
        private bool isStandby;
        private bool isWorking;
        private IFaultTolerantService serviceProxy;
        private Timer heartbeatTimer;
        private Timer workTimer;
        private DuplexChannelFactory<IFaultTolerantService> channelFactory;
        public CryptoManager cryptoManager { get; private set; }
        private DateTime startTime;
        private int workOutputCount = 0;
        private int heartbeatCount = 0;

        public ClientApplication(string clientId, bool isStandby)
        {
            this.ClientId = clientId;
            this.isStandby = isStandby;
            this.isWorking = false;
            this.cryptoManager = new CryptoManager();
            this.startTime = DateTime.Now;
        }

        public void Start()
        {
            try
            {
                ShowClientHeader();

                // Create callback instance
                var callback = new ClientCallback(this);
                var instanceContext = new InstanceContext(callback);

                // Create channel factory
                var binding = new WSDualHttpBinding();
                binding.Security.Mode = WSDualHttpSecurityMode.None;
                var endpointAddress = new EndpointAddress("http://localhost:8080/FaultTolerantService");

                channelFactory = new DuplexChannelFactory<FaultTolerantSystem.IFaultTolerantService>(
                    instanceContext, binding, endpointAddress);

                serviceProxy = channelFactory.CreateChannel();

                // Register with server
                Console.WriteLine($"[{ClientId}] Connecting to server...");
                var result = serviceProxy.RegisterClient(ClientId, isStandby);
                Console.WriteLine($"[{ClientId}] ✓ {result}");

                // Start heartbeat timer
                heartbeatTimer = new Timer(SendHeartbeat, null,
                    TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));

                // If not standby, start working immediately
                if (!isStandby)
                {
                    StartWorking();
                }
                else
                {
                    Console.WriteLine($"[{ClientId}] Status: STANDBY MODE - Waiting for activation signal");
                    Console.WriteLine($"[{ClientId}] Ready to take over if working client fails");
                }

                ShowInstructions();

                // Keep the client running and handle user input
                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine($"\n[{ClientId}] Shutdown requested by user");
                        break;
                    }
                    else if (key.Key == ConsoleKey.S)
                    {
                        SendEncryptedMessage();
                    }
                    else if (key.Key == ConsoleKey.F)
                    {
                        SimulateFailure();
                    }
                    else if (key.Key == ConsoleKey.I)
                    {
                        ShowClientInfo();
                    }
                }

                Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[{ClientId} ERROR] {ex.Message}");
                Console.WriteLine("\nPossible causes:");
                Console.WriteLine("  - Server is not running");
                Console.WriteLine("  - Network connection issues");
                Console.WriteLine("  - Port 8080 is not accessible");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        private void ShowClientHeader()
        {
            var modeText = isStandby ? "STANDBY CLIENT" : "WORKING CLIENT";
            var modeIcon = isStandby ? "" : "";

            Console.WriteLine("====================================================");
            Console.WriteLine($"      {modeIcon} {modeText}: {ClientId}");
            Console.WriteLine("====================================================");
            Console.WriteLine();
        }

        private void ShowInstructions()
        {
            Console.WriteLine("\n" + new string('-', 50));
            Console.WriteLine("                 CLIENT COMMANDS");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("  Q - Quit client");
            Console.WriteLine("  F - Simulate failure (stop heartbeats)");
            Console.WriteLine("  S - Send encrypted message to another client");
            Console.WriteLine("  I - Show client information");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine();
        }

        private void ShowClientInfo()
        {
            var uptime = DateTime.Now - startTime;
            var status = isWorking ? "WORKING" : (isStandby ? "STANDBY" : "INACTIVE");
            var statusIcon = isWorking ? "" : (isStandby ? "" : "");

            Console.WriteLine($"\n" + new string('=', 40));
            Console.WriteLine($"        CLIENT INFORMATION");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine($"Client ID: {ClientId}");
            Console.WriteLine($"Status: {statusIcon} {status}");
            Console.WriteLine($"Uptime: {uptime:hh\\:mm\\:ss}");
            Console.WriteLine($"Heartbeats sent: {heartbeatCount}");
            if (isWorking)
            {
                Console.WriteLine($"Work outputs: {workOutputCount}");
            }
            Console.WriteLine($"Started: {startTime:HH:mm:ss}");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine();
        }

        public void StartWorking()
        {
            if (!isWorking)
            {
                isWorking = true;
                isStandby = false;
                workOutputCount = 0;

                Console.WriteLine($"\n[{ClientId}] ACTIVATED - Now working!");
                Console.WriteLine($"[{ClientId}] Will output 'WORKING...' every 5 seconds");

                // Start work timer - output every 5 seconds as per specification
                workTimer = new Timer(DoWork, null,
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
            }
        }

        public void StopWorking()
        {
            if (isWorking)
            {
                isWorking = false;
                Console.WriteLine($"\n[{ClientId}] DEACTIVATED - Stopped working");

                // Stop work timer
                workTimer?.Change(Timeout.Infinite, 0);
                workTimer?.Dispose();
                workTimer = null;
            }
        }

        private void DoWork(object state)
        {
            if (isWorking)
            {
                workOutputCount++;
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                // Output format as specified: "WORKING... <timestamp>"
                Console.WriteLine($"[{ClientId}] WORKING... {timestamp}");
            }
        }

        private void SendHeartbeat(object state)
        {
            try
            {
                serviceProxy.SendHeartbeat(ClientId);
                heartbeatCount++;

                var now = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{ClientId}] Heartbeat #{heartbeatCount} sent at {now}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId}] Failed to send heartbeat: {ex.Message}");
            }
        }

        private void SendEncryptedMessage()
        {
            try
            {
                Console.WriteLine($"\n" + new string('-', 30));
                Console.WriteLine("    ENCRYPTED MESSAGE");
                Console.WriteLine(new string('-', 30));

                Console.Write("Enter receiver client ID (e.g., CLIENT2): ");
                var receiverId = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(receiverId))
                {
                    Console.WriteLine("Invalid receiver ID");
                    return;
                }

                Console.Write("Enter message: ");
                var message = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(message))
                {
                    Console.WriteLine("Message cannot be empty");
                    return;
                }

                Console.WriteLine("Encrypting message...");
                var encryptedData = cryptoManager.Encrypt(message);

                Console.WriteLine("Sending encrypted message to server...");
                serviceProxy.GetEncryptedMessage(ClientId, receiverId, encryptedData);

                Console.WriteLine($"Encrypted message sent successfully to {receiverId}");
                Console.WriteLine(new string('-', 30));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId}] Failed to send message: {ex.Message}");
            }
        }

        private void SimulateFailure()
        {
            Console.WriteLine($"\n[{ClientId}] SIMULATING FAILURE");
            Console.WriteLine($"[{ClientId}] Stopping heartbeats to simulate network/system failure...");
            Console.WriteLine($"[{ClientId}] Server will detect failure in ~30 seconds");
            Console.WriteLine($"[{ClientId}] If this is a working client, standby will be activated");

            // Stop heartbeat timer to simulate failure
            heartbeatTimer?.Change(Timeout.Infinite, 0);
        }

        private void Shutdown()
        {
            try
            {
                Console.WriteLine($"\n[{ClientId}] Shutting down client...");

                // Stop timers
                heartbeatTimer?.Change(Timeout.Infinite, 0);
                heartbeatTimer?.Dispose();

                workTimer?.Change(Timeout.Infinite, 0);
                workTimer?.Dispose();

                // Unregister from server
                if (serviceProxy != null)
                {
                    try
                    {
                        serviceProxy.UnregisterClient(ClientId);
                        Console.WriteLine($"[{ClientId}] Unregistered from server");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{ClientId}] Warning: Could not unregister cleanly: {ex.Message}");
                    }
                }

                // Close channel
                channelFactory?.Close();
                Console.WriteLine($"[{ClientId}] Connection closed");
                Console.WriteLine($"[{ClientId}] Goodbye!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId}] Error during shutdown: {ex.Message}");
            }
        }
    }

    // Cryptography Manager for secure communication between clients
    public class CryptoManager
    {
        private readonly byte[] key;
        private readonly byte[] iv;

        public CryptoManager()
        {
            key = Encoding.UTF8.GetBytes("FaultTolerantSystemSecretKey1234"); // 32 bytes for AES-256
            iv = Encoding.UTF8.GetBytes("InitializationV!"); // 16 bytes for AES IV
        }

        public byte[] Encrypt(string plainText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }
            }
        }

        public string Decrypt(byte[] cipherText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }
    }
}