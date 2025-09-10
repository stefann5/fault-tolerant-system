using System;
using System.Data.SqlClient;
using System.Configuration;

namespace FaultTolerantSystem
{
    public class DatabaseManager
    {
        private string connectionString;

        public DatabaseManager()
        {
            // Use LocalDB for simplicity (can be changed to SQL Server)
            connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=FaultTolerantDB;Integrated Security=True";
        }

        public void InitializeDatabase()
        {
            try
            {
                // Create database if not exists
                using (var connection = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True"))
                {
                    connection.Open();

                    // Create database
                    var createDbCmd = new SqlCommand(
                        @"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FaultTolerantDB')
                          CREATE DATABASE FaultTolerantDB", connection);
                    createDbCmd.ExecuteNonQuery();
                }

                // Create tables
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Create Clients table
                    var createTableCmd = new SqlCommand(
                        @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Clients' AND xtype='U')
                          CREATE TABLE Clients (
                              ClientId NVARCHAR(50) PRIMARY KEY,
                              Status NVARCHAR(20),
                              LastHeartbeat DATETIME,
                              RegisteredAt DATETIME,
                              IsStandby BIT
                          )", connection);
                    createTableCmd.ExecuteNonQuery();

                    // Create ClientLogs table for history
                    var createLogsCmd = new SqlCommand(
                        @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ClientLogs' AND xtype='U')
                          CREATE TABLE ClientLogs (
                              LogId INT IDENTITY(1,1) PRIMARY KEY,
                              ClientId NVARCHAR(50),
                              Event NVARCHAR(100),
                              Timestamp DATETIME,
                              Details NVARCHAR(MAX)
                          )", connection);
                    createLogsCmd.ExecuteNonQuery();

                    Console.WriteLine("[DATABASE] Database initialized successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATABASE ERROR] Failed to initialize database: {ex.Message}");
            }
        }

        public void SaveClient(ClientSession client)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Insert or update client
                    var cmd = new SqlCommand(
                        @"IF EXISTS (SELECT 1 FROM Clients WHERE ClientId = @ClientId)
                          UPDATE Clients 
                          SET Status = @Status, LastHeartbeat = @LastHeartbeat, IsStandby = @IsStandby
                          WHERE ClientId = @ClientId
                          ELSE
                          INSERT INTO Clients (ClientId, Status, LastHeartbeat, RegisteredAt, IsStandby)
                          VALUES (@ClientId, @Status, @LastHeartbeat, @RegisteredAt, @IsStandby)", connection);

                    cmd.Parameters.AddWithValue("@ClientId", client.ClientId);
                    cmd.Parameters.AddWithValue("@Status", client.Status.ToString());
                    cmd.Parameters.AddWithValue("@LastHeartbeat", client.LastHeartbeat);
                    cmd.Parameters.AddWithValue("@RegisteredAt", client.RegisteredAt);
                    cmd.Parameters.AddWithValue("@IsStandby", client.IsStandby);

                    cmd.ExecuteNonQuery();

                    // Log the event
                    LogEvent(client.ClientId, "CLIENT_REGISTERED",
                        $"Client registered as {(client.IsStandby ? "STANDBY" : "WORKING")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATABASE ERROR] Failed to save client: {ex.Message}");
            }
        }

        public void UpdateHeartbeat(string clientId, DateTime heartbeatTime)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var cmd = new SqlCommand(
                        @"UPDATE Clients 
                          SET LastHeartbeat = @LastHeartbeat 
                          WHERE ClientId = @ClientId", connection);

                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@LastHeartbeat", heartbeatTime);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATABASE ERROR] Failed to update heartbeat: {ex.Message}");
            }
        }

        public void UpdateClientStatus(string clientId, ClientStatus status)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var cmd = new SqlCommand(
                        @"UPDATE Clients 
                          SET Status = @Status 
                          WHERE ClientId = @ClientId", connection);

                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@Status", status.ToString());

                    cmd.ExecuteNonQuery();

                    // Log status change
                    LogEvent(clientId, "STATUS_CHANGED", $"Status changed to {status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATABASE ERROR] Failed to update client status: {ex.Message}");
            }
        }

        public void LogEvent(string clientId, string eventType, string details)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var cmd = new SqlCommand(
                        @"INSERT INTO ClientLogs (ClientId, Event, Timestamp, Details)
                          VALUES (@ClientId, @Event, @Timestamp, @Details)", connection);

                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@Event", eventType);
                    cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Details", details ?? "");

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DATABASE ERROR] Failed to log event: {ex.Message}");
            }
        }
    }
}