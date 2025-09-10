using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;

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
            dbManager = new DatabaseManager();
            dbManager.InitializeDatabase();

            // Start heartbeat monitoring timer
            heartbeatCheckTimer = new Timer(CheckHeartbeats, null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public string RegisterClient(string clientId, bool isStandby)
        {
            lock (lockObject)
            {
                try
                {
                    var callback = OperationContext.Current.GetCallbackChannel<IClientCallback>();

                    if (clients.ContainsKey(clientId))
                    {
                        clients[clientId].Callback = callback;
                        clients[clientId].LastHeartbeat = DateTime.Now;
                        Console.WriteLine($"[SERVER] Client {clientId} reconnected");
                    }
                    else
                    {
                        var session = new ClientSession
                        {
                            ClientId = clientId,
                            Callback = callback,
                            Status = isStandby ? ClientStatus.Standby : ClientStatus.Working,
                            LastHeartbeat = DateTime.Now,
                            RegisteredAt = DateTime.Now,
                            IsStandby = isStandby
                        };

                        clients.Add(clientId, session);

                        // Save to database
                        dbManager.SaveClient(session);

                        Console.WriteLine($"[SERVER] Client {clientId} registered as {(isStandby ? "STANDBY" : "WORKING")}");

                        // If not standby, start working immediately
                        if (!isStandby)
                        {
                            callback.StartWorking();
                        }
                    }

                    return "Registration successful";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVER ERROR] Registration failed: {ex.Message}");
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
                    clients[clientId].LastHeartbeat = DateTime.Now;
                    clients[clientId].Status = clients[clientId].IsStandby ?
                        ClientStatus.Standby : ClientStatus.Working;

                    // Update database
                    dbManager.UpdateHeartbeat(clientId, DateTime.Now);

                    Console.WriteLine($"[SERVER] Heartbeat received from {clientId}");
                }
            }
        }

        public void UnregisterClient(string clientId)
        {
            lock (lockObject)
            {
                if (clients.ContainsKey(clientId))
                {
                    clients[clientId].Status = ClientStatus.Dead;
                    dbManager.UpdateClientStatus(clientId, ClientStatus.Dead);
                    clients.Remove(clientId);
                    Console.WriteLine($"[SERVER] Client {clientId} unregistered");
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
                        Console.WriteLine($"[SERVER] Message forwarded from {senderId} to {receiverId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SERVER ERROR] Failed to forward message: {ex.Message}");
                    }
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
                    Console.WriteLine($"[SERVER] Simulating failure for client {clientId}");
                    clients[clientId].LastHeartbeat = DateTime.Now.AddSeconds(-31);
                    CheckHeartbeats(null);
                }
            }
        }

        private void CheckHeartbeats(object state)
        {
            lock (lockObject)
            {
                var now = DateTime.Now;
                var deadClients = new List<string>();

                foreach (var client in clients.Values)
                {
                    var timeSinceLastHeartbeat = (now - client.LastHeartbeat).TotalSeconds;

                    if (timeSinceLastHeartbeat > heartbeatTimeout &&
                        client.Status != ClientStatus.Dead)
                    {
                        Console.WriteLine($"[SERVER] Client {client.ClientId} is DEAD (no heartbeat for {timeSinceLastHeartbeat:F0} seconds)");
                        client.Status = ClientStatus.Dead;
                        dbManager.UpdateClientStatus(client.ClientId, ClientStatus.Dead);
                        deadClients.Add(client.ClientId);
                    }
                }

                // Handle failover for dead working clients
                foreach (var deadClientId in deadClients)
                {
                    var deadClient = clients[deadClientId];
                    if (!deadClient.IsStandby)
                    {
                        ActivateStandbyClient();
                    }
                    clients.Remove(deadClientId);
                }
            }
        }

        private void ActivateStandbyClient()
        {
            var standbyClient = clients.Values
                .FirstOrDefault(c => c.IsStandby && c.Status == ClientStatus.Standby);

            if (standbyClient != null)
            {
                try
                {
                    Console.WriteLine($"[SERVER] Activating standby client {standbyClient.ClientId}");
                    standbyClient.IsStandby = false;
                    standbyClient.Status = ClientStatus.Working;
                    standbyClient.Callback.StartWorking();

                    dbManager.UpdateClientStatus(standbyClient.ClientId, ClientStatus.Working);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVER ERROR] Failed to activate standby client: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[SERVER WARNING] No standby clients available for failover!");
            }
        }
    }

    // Client Session class
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