using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
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
            Console.WriteLine($"[{client.ClientId}] Received START WORKING signal from server");
            client.StartWorking();
        }

        public void StopWorking()
        {
            Console.WriteLine($"[{client.ClientId}] Received STOP WORKING signal from server");
            client.StopWorking();
        }

        public void ReceiveMessage(byte[] encryptedData, string senderId)
        {
            var decryptedMessage = client.cryptoManager.Decrypt(encryptedData);
            Console.WriteLine($"[{client.ClientId}] Received encrypted message from {senderId}: {decryptedMessage}");
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

        public ClientApplication(string clientId, bool isStandby)
        {
            this.ClientId = clientId;
            this.isStandby = isStandby;
            this.isWorking = false;
            this.cryptoManager = new CryptoManager();
        }

        public void Start()
        {
            try
            {
                Console.WriteLine($"[{ClientId}] Starting client application...");

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
                var result = serviceProxy.RegisterClient(ClientId, isStandby);
                Console.WriteLine($"[{ClientId}] {result}");

                // Start heartbeat timer
                heartbeatTimer = new Timer(SendHeartbeat, null,
                    TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));

                // If not standby, start working immediately
                if (!isStandby)
                {
                    StartWorking();
                }
                else
                {
                    Console.WriteLine($"[{ClientId}] Running in STANDBY mode");
                }

                // Keep the client running
                Console.WriteLine($"[{ClientId}] Press 'Q' to quit, 'S' to send message, 'F' to simulate failure");

                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Q)
                    {
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
                }

                Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId} ERROR] {ex.Message}");
            }
        }

        public void StartWorking()
        {
            if (!isWorking)
            {
                isWorking = true;
                isStandby = false;
                Console.WriteLine($"[{ClientId}] Starting work...");

                // Start work timer
                workTimer = new Timer(DoWork, null,
                    TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            }
        }

        public void StopWorking()
        {
            if (isWorking)
            {
                isWorking = false;
                Console.WriteLine($"[{ClientId}] Stopping work...");

                // Stop work timer
                workTimer?.Change(Timeout.Infinite, 0);
                workTimer?.Dispose();
            }
        }

        private void DoWork(object state)
        {
            if (isWorking)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{ClientId}] WORKING... {timestamp}");
            }
        }

        private void SendHeartbeat(object state)
        {
            try
            {
                serviceProxy.SendHeartbeat(ClientId);
                Console.WriteLine($"[{ClientId}] Heartbeat sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId} ERROR] Failed to send heartbeat: {ex.Message}");
            }
        }

        private void SendEncryptedMessage()
        {
            try
            {
                Console.Write("Enter receiver client ID: ");
                var receiverId = Console.ReadLine();

                Console.Write("Enter message: ");
                var message = Console.ReadLine();

                var encryptedData = cryptoManager.Encrypt(message);
                serviceProxy.GetEncryptedMessage(ClientId, receiverId, encryptedData);

                Console.WriteLine($"[{ClientId}] Encrypted message sent to {receiverId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId} ERROR] Failed to send message: {ex.Message}");
            }
        }

        private void SimulateFailure()
        {
            Console.WriteLine($"[{ClientId}] Simulating failure - stopping heartbeats...");
            heartbeatTimer?.Change(Timeout.Infinite, 0);
        }

        private void Shutdown()
        {
            try
            {
                Console.WriteLine($"[{ClientId}] Shutting down...");

                // Stop timers
                heartbeatTimer?.Change(Timeout.Infinite, 0);
                heartbeatTimer?.Dispose();

                workTimer?.Change(Timeout.Infinite, 0);
                workTimer?.Dispose();

                // Unregister from server
                if (serviceProxy != null)
                {
                    serviceProxy.UnregisterClient(ClientId);
                }

                // Close channel
                channelFactory?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId} ERROR] Error during shutdown: {ex.Message}");
            }
        }
    }

    // Cryptography Manager for secure communication
    public class CryptoManager
    {
        private readonly byte[] key;
        private readonly byte[] iv;

        public CryptoManager()
        {
            // In production, use a secure key exchange mechanism
            // For demo purposes, using a fixed key
            key = Encoding.UTF8.GetBytes("ThisIsASecretKey1234567890123456");
            iv = Encoding.UTF8.GetBytes("ThisIsAnIV123456");
        }

        public byte[] Encrypt(string plainText)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

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

                using (var decryptor = aes.CreateDecryptor())
                {
                    var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }
    }
}