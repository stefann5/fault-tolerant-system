using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;

namespace FaultTolerantSystem
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
                     ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class FaultTolerantService : IFaultTolerantService
    {
        private readonly Dictionary<string, ClientSession> clients = new Dictionary<string, ClientSession>();
        private readonly object lockObject = new object();
        private Timer heartbeatCheckTimer;
        private readonly int heartbeatTimeout = 30; // seconds
        private readonly DatabaseManager dbManager;

        public FaultTolerantService()
        {
            Console.WriteLine("[SERVICE] Initializing Fault Tolerant Service...");

            dbManager = new DatabaseManager();
            dbManager.InitializeDatabase();

            // Start heartbeat monitoring timer - check every 5 seconds
            heartbeatCheckTimer = new Timer(CheckHeartbeats, null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            Console.WriteLine("[SERVICE] Heartbeat monitoring started (30s timeout)");
            Console.WriteLine("[SERVICE] Database initialized");
            Console.WriteLine("[SERVICE] Failover mechanism ready");
        }

        public string RegisterClient(string clientId, bool isStandby)
        {
            lock (lockObject)
            {
                try
                {
                    var callback = OperationContext.Current.GetCallbackChannel<IClientCallback>();
                    var now = DateTime.Now;

                    if (clients.ContainsKey(clientId))
                    {
                        // Client reconnecting
                        clients[clientId].Callback = callback;
                        clients[clientId].LastHeartbeat = now;
                        clients[clientId].Status = isStandby ? ClientStatus.Standby : ClientStatus.Working;

                        Console.WriteLine($"[SERVICE] {clientId} reconnected ({(isStandby ? "STANDBY" : "WORKING")})");
                    }
                    else
                    {
                        // New client registration
                        var session = new ClientSession
                        {
                            ClientId = clientId,
                            Callback = callback,
                            Status = isStandby ? ClientStatus.Standby : ClientStatus.Working,
                            LastHeartbeat = now,
                            RegisteredAt = now,
                            IsStandby = isStandby
                        };

                        clients.Add(clientId, session);
                        dbManager.SaveClient(session);

                        var statusIcon =  "";
                        var statusText = isStandby ? "STANDBY" : "WORKING";

                        Console.WriteLine($"{statusIcon} [SERVICE] {clientId} registered as {statusText}");

                        // If working client, start it immediately
                        if (!isStandby)
                        {
                            Console.WriteLine($"[SERVICE] Activating {clientId} for work");
                            callback.StartWorking();
                        }

                        ShowSystemStatus();
                    }

                    return $"Registration successful - {clientId} is now {(isStandby ? "STANDBY" : "WORKING")}";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVICE ERROR] Registration failed for {clientId}: {ex.Message}");
                    return $"Registration failed: {ex.Message}";
                }
            }
        }

        public void SendHeartbeat(string clientId)
        {
            lock (lockObject)
            {
                if (clients.ContainsKey(clientId))
                {
                    var client = clients[clientId];
                    client.LastHeartbeat = DateTime.Now;

                    // Update status based on current mode
                    client.Status = client.IsStandby ? ClientStatus.Standby : ClientStatus.Working;

                    // Update database
                    dbManager.UpdateHeartbeat(clientId, DateTime.Now);

                    // Only show heartbeat for active monitoring - less verbose
                    var icon = client.IsStandby ? "" : "";
                    Console.WriteLine($"{icon} [HEARTBEAT] {clientId} alive");
                }
                else
                {
                    Console.WriteLine($"[SERVICE] Heartbeat from unregistered client: {clientId}");
                }
            }
        }

        public void UnregisterClient(string clientId)
        {
            lock (lockObject)
            {
                if (clients.ContainsKey(clientId))
                {
                    var client = clients[clientId];
                    client.Status = ClientStatus.Dead;
                    dbManager.UpdateClientStatus(clientId, ClientStatus.Dead);
                    dbManager.LogEvent(clientId, "CLIENT_UNREGISTERED", "Client gracefully disconnected");

                    clients.Remove(clientId);
                    Console.WriteLine($"[SERVICE] {clientId} unregistered (graceful shutdown)");

                    ShowSystemStatus();
                }
            }
        }

        public byte[] GetEncryptedMessage(string senderId, string receiverId, byte[] encryptedData)
        {
            lock (lockObject)
            {
                if (clients.ContainsKey(receiverId))
                {
                    try
                    {
                        clients[receiverId].Callback.ReceiveMessage(encryptedData, senderId);
                        Console.WriteLine($"[MESSAGE] Encrypted message forwarded: {senderId} → {receiverId}");

                        dbManager.LogEvent(senderId, "MESSAGE_SENT", $"Encrypted message sent to {receiverId}");
                        dbManager.LogEvent(receiverId, "MESSAGE_RECEIVED", $"Encrypted message received from {senderId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MESSAGE ERROR] Failed to forward {senderId} → {receiverId}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[MESSAGE] Target client {receiverId} not found for message from {senderId}");
                }
                return encryptedData;
            }
        }

        public ClientInfo[] GetAllClients()
        {
            lock (lockObject)
            {
                return clients.Values.Select(c => new ClientInfo
                {
                    ClientId = c.ClientId,
                    Status = c.Status,
                    LastHeartbeat = c.LastHeartbeat,
                    IsStandby = c.IsStandby,
                    RegisteredAt = c.RegisteredAt
                }).ToArray();
            }
        }

        public void SimulateFailure(string clientId)
        {
            lock (lockObject)
            {
                if (clients.ContainsKey(clientId))
                {
                    Console.WriteLine($"[SIMULATION] Forcing failure for {clientId}");
                    clients[clientId].LastHeartbeat = DateTime.Now.AddSeconds(-31);
                    dbManager.LogEvent(clientId, "FAILURE_SIMULATED", "Manual failure simulation triggered");

                    // Trigger immediate check
                    CheckHeartbeats(null);
                }
                else
                {
                    Console.WriteLine($"[SIMULATION] Client {clientId} not found for failure simulation");
                }
            }
        }

        private void CheckHeartbeats(object state)
        {
            lock (lockObject)
            {
                var now = DateTime.Now;
                var deadClients = new List<ClientSession>();

                foreach (var client in clients.Values)
                {
                    var timeSinceLastHeartbeat = (now - client.LastHeartbeat).TotalSeconds;

                    // Check if client exceeded timeout and is not already marked as dead
                    if (timeSinceLastHeartbeat > heartbeatTimeout && client.Status != ClientStatus.Dead)
                    {
                        Console.WriteLine($"[TIMEOUT] {client.ClientId} DEAD (no heartbeat for {timeSinceLastHeartbeat:F0}s)");

                        client.Status = ClientStatus.Dead;
                        dbManager.UpdateClientStatus(client.ClientId, ClientStatus.Dead);
                        dbManager.LogEvent(client.ClientId, "CLIENT_TIMEOUT", $"No heartbeat for {timeSinceLastHeartbeat:F0} seconds");

                        deadClients.Add(client);
                    }
                }

                // Handle failover for dead working clients
                foreach (var deadClient in deadClients)
                {
                    if (!deadClient.IsStandby)
                    {
                        Console.WriteLine($"[FAILOVER] Working client {deadClient.ClientId} failed - initiating failover");
                        ActivateStandbyClient(deadClient.ClientId);
                    }
                    else
                    {
                        Console.WriteLine($"[TIMEOUT] Standby client {deadClient.ClientId} failed");
                    }

                    clients.Remove(deadClient.ClientId);
                }

                if (deadClients.Count > 0)
                {
                    ShowSystemStatus();
                }
            }
        }

        private void ActivateStandbyClient(string failedClientId = null)
        {
            // Find an available standby client
            var standbyClient = clients.Values
                .Where(c => c.IsStandby && c.Status == ClientStatus.Standby)
                .OrderBy(c => c.RegisteredAt) // Activate oldest standby first
                .FirstOrDefault();

            if (standbyClient != null)
            {
                try
                {
                    Console.WriteLine($"[FAILOVER] Activating standby client {standbyClient.ClientId}");

                    standbyClient.IsStandby = false;
                    standbyClient.Status = ClientStatus.Working;

                    // Send activation signal
                    standbyClient.Callback.StartWorking();

                    // Update database
                    dbManager.UpdateClientStatus(standbyClient.ClientId, ClientStatus.Working);
                    dbManager.LogEvent(standbyClient.ClientId, "ACTIVATED_FROM_STANDBY",
                        failedClientId != null ? $"Activated due to {failedClientId} failure" : "Activated from standby");

                    Console.WriteLine($"[FAILOVER] {standbyClient.ClientId} successfully activated as working client");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FAILOVER ERROR] Failed to activate standby client {standbyClient.ClientId}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[CRITICAL] NO STANDBY CLIENTS AVAILABLE FOR FAILOVER!");
                Console.WriteLine($"[CRITICAL] System redundancy compromised - consider adding more clients");
            }
        }

        private void ShowSystemStatus()
        {
            var workingCount = clients.Values.Count(c => !c.IsStandby && c.Status == ClientStatus.Working);
            var standbyCount = clients.Values.Count(c => c.IsStandby && c.Status == ClientStatus.Standby);
            var totalCount = clients.Count;

            Console.WriteLine($"\n[SYSTEM STATUS] Total: {totalCount}, Working: {workingCount}, Standby: {standbyCount}");

            if (workingCount == 0)
            {
                Console.WriteLine("[CRITICAL] NO WORKING CLIENTS!");
            }
            else if (workingCount < 2 && standbyCount == 0)
            {
                Console.WriteLine("[WARNING] Low redundancy - consider adding standby clients");
            }
            else
            {
                Console.WriteLine("[STATUS] System operating normally");
            }
        }
    }

    // Client Session Management
    public class ClientSession
    {
        public string ClientId { get; set; }
        public IClientCallback Callback { get; set; }
        public ClientStatus Status { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public DateTime RegisteredAt { get; set; }
        public bool IsStandby { get; set; }
    }
}