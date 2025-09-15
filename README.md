# Fault Tolerant System - Complete Documentation

## Table of Contents
1. [Project Overview](#1-project-overview)
2. [System Architecture](#2-system-architecture)
3. [Code Architecture](#3-code-architecture)
4. [Class Documentation](#4-class-documentation)
5. [Database Schema](#5-database-schema)
6. [Security Implementation](#6-security-implementation)
7. [Configuration Guide](#7-configuration-guide)
8. [User Guide](#8-user-guide)
9. [Development Guide](#9-development-guide)
10. [Quick Reference](#10-quick-reference)

---

## 1. Project Overview

### 1.1 Purpose
The Fault Tolerant System demonstrates enterprise-level fault tolerance using WCF, featuring automatic failover, heartbeat monitoring, and secure communication between multiple client applications.

### 1.2 Key Features
- **2 Working Clients + 2 Standby Clients** - Automatic system launch  
- **Heartbeat Monitoring** - 10-second intervals with 30-second timeout  
- **Automatic Failover** - Standby clients activate when working clients fail  
- **AES-256 Encryption** - Secure inter-client messaging  
- **Database Logging** - Complete audit trail in SQL Server LocalDB  

### 1.3 System Requirements
- **OS**: Windows 10/11 or Windows Server 2016+
- **Framework**: .NET Framework 4.7.2+
- **Database**: SQL Server LocalDB (included with Visual Studio)
- **Memory**: 4 GB RAM minimum, 8 GB recommended
- **Network**: Port 8080 available

### 1.4 Technology Stack
- **Communication**: WCF (Windows Communication Foundation)
- **Binding**: WSDualHttpBinding for bidirectional communication
- **Database**: SQL Server LocalDB with Entity Framework-style access
- **Encryption**: AES-256-CBC with PKCS7 padding
- **Concurrency**: Thread-safe collections and locking mechanisms

---

## 2. System Architecture

### 2.1 High-Level Architecture
```
┌─────────────┐    ┌─────────────────────────┐    ┌─────────┐
│   CLIENT1   │◄──►│                         │◄──►│         │
│ (Working)   │    │       WCF SERVER        │    │ LocalDB │
└─────────────┘    │                         │    │         │
┌─────────────┐    │  • Heartbeat Monitor    │    │ Tables: │
│   CLIENT2   │◄──►│  • Failover Manager     │◄──►│ -Clients│
│ (Working)   │    │  • Message Router       │    │ -Logs   │
└─────────────┘    │  • Database Manager     │    │         │
┌─────────────┐    │                         │    └─────────┘
│   CLIENT3   │◄──►│  Port: 8080             │
│ (Standby)   │    │  Protocol: HTTP         │
└─────────────┘    │  Binding: WSDualHttp    │
┌─────────────┐    │                         │
│   CLIENT4   │◄──►│                         │
│ (Standby)   │    └─────────────────────────┘
└─────────────┘
```

### 2.2 Communication Flow
1. **Client Registration**: Clients register with server specifying working/standby mode
2. **Heartbeat Loop**: Clients send heartbeats every 10 seconds
3. **Work Execution**: Working clients output "WORKING..." every 5 seconds
4. **Failure Detection**: Server detects missed heartbeats after 30 seconds
5. **Automatic Failover**: Server activates standby clients when working clients fail
6. **Message Routing**: Server routes encrypted messages between clients

### 2.3 Fault Tolerance Process
1. **Normal Operation**: Working clients send heartbeats every 10 seconds
2. **Failure Detection**: Server marks clients dead after 30 seconds without heartbeat
3. **Automatic Failover**: Server activates oldest standby client
4. **Recovery**: New working client begins operation within 1 second

---

## 3. Code Architecture

### 3.1 Project Structure
```
FaultTolerantSystem/
├── Client/
│   └── ClientApplication.cs        # Client implementation and logic
├── Server/
│   ├── FaultTolerantService.cs     # Core WCF service implementation
│   ├── ServerHost.cs               # WCF service hosting and management
│   └── DatabaseManager.cs          # Database operations and persistence
├── Common/
│   └── IFaultTolerantService.cs    # WCF service and data contracts
├── MainLauncher.cs                 # Application entry point and launcher
├── App.config                      # WCF and database configuration
└── Properties/
    └── AssemblyInfo.cs             # Assembly metadata
```

### 3.2 Design Patterns
- **Singleton Pattern**: Service instance for maintaining client state
- **Observer Pattern**: Callback mechanism for server-to-client communication
- **Factory Pattern**: Channel factory for WCF client connections
- **Repository Pattern**: Database manager for data persistence
- **Strategy Pattern**: Different client behaviors (working vs standby)

### 3.3 Threading Model
- **Server**: Multi-threaded with thread-safe collections and locking
- **Clients**: Single-threaded with timer-based operations
- **Timers**: Separate threads for heartbeats and work execution
- **Callbacks**: Reentrant callback behavior for server signals

---

## 4. Class Documentation

### 4.1 Core Interfaces

#### IFaultTolerantService
Main WCF service contract defining client-server communication.

```csharp
[ServiceContract(CallbackContract = typeof(IClientCallback))]
public interface IFaultTolerantService
```

**Methods:**
- `RegisterClient(string clientId, bool isStandby)` - Registers a new client
- `SendHeartbeat(string clientId)` - Receives heartbeat from client
- `UnregisterClient(string clientId)` - Gracefully removes client
- `GetEncryptedMessage(...)` - Routes encrypted messages between clients
- `GetAllClients()` - Returns all connected client information
- `SimulateFailure(string clientId)` - Forces client failure for testing

#### IClientCallback
Callback contract for server-to-client communication.

```csharp
[ServiceContract]
public interface IClientCallback
```

**Methods:**
- `StartWorking()` - Signals client to begin work output
- `StopWorking()` - Signals client to stop work output
- `ReceiveMessage(byte[] encryptedData, string senderId)` - Delivers encrypted message

### 4.2 Server Classes

#### FaultTolerantService
Core service implementation handling all client management and fault tolerance.

```csharp
[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
                 ConcurrencyMode = ConcurrencyMode.Multiple)]
public class FaultTolerantService : IFaultTolerantService
```

**Key Fields:**
- `Dictionary<string, ClientSession> clients` - Thread-safe client registry
- `Timer heartbeatCheckTimer` - Monitors client heartbeats every 5 seconds
- `DatabaseManager dbManager` - Handles all database operations
- `object lockObject` - Synchronization for thread safety

**Key Methods:**
- `RegisterClient()` - Manages client registration and reconnection
- `CheckHeartbeats()` - Timer callback for failure detection
- `ActivateStandbyClient()` - Implements failover logic
- `ShowSystemStatus()` - Displays current system health

**Concurrency Design:**
- Uses `ConcurrencyMode.Multiple` for high throughput
- All client operations protected by `lock(lockObject)`
- Database operations are thread-safe
- Timer callbacks synchronized with main operations

#### ServerHost
WCF service hosting and management with administrative interface.

```csharp
public class ServerHost
```

**Key Features:**
- Programmatic WCF configuration (no app.config dependency)
- Administrative command interface (L, S, Q commands)
- Service lifecycle management
- Error handling and diagnostics

**Configuration:**
- `WSDualHttpBinding` with no security for demo purposes
- Maximum message sizes for large encrypted payloads
- Metadata publishing enabled for client discovery

#### DatabaseManager
Handles all database operations with LocalDB integration.

```csharp
public class DatabaseManager
```

**Key Methods:**
- `InitializeDatabase()` - Creates database and tables if not exists
- `SaveClient()` - Persists client registration information
- `UpdateHeartbeat()` - Updates last heartbeat timestamp
- `UpdateClientStatus()` - Changes client status (Working/Standby/Dead)
- `LogEvent()` - Records system events for audit trail

**Database Operations:**
- Uses parameterized queries to prevent SQL injection
- Handles connection management automatically
- Graceful error handling with console logging
- Supports both insert and update operations

#### ClientSession
Internal class representing connected client state.

```csharp
public class ClientSession
```

**Properties:**
- `ClientId` - Unique identifier for the client
- `Callback` - WCF callback channel for server-to-client communication
- `Status` - Current status (Working/Standby/Dead)
- `LastHeartbeat` - Timestamp of last received heartbeat
- `RegisteredAt` - Initial registration time
- `IsStandby` - Whether client is in standby mode

### 4.3 Client Classes

#### ClientApplication
Main client implementation handling all client-side logic.

```csharp
public class ClientApplication
```

**Key Features:**
- Dual-mode operation (working vs standby)
- Automatic heartbeat transmission
- Work output generation
- Encrypted messaging capability
- Failure simulation

**Timers:**
- `heartbeatTimer` - Sends heartbeats every 10 seconds
- `workTimer` - Outputs "WORKING..." every 5 seconds (working clients only)

**Key Methods:**
- `Start()` - Initializes WCF connection and starts operation
- `StartWorking()` - Transitions from standby to working mode
- `StopWorking()` - Stops work output (returns to standby)
- `SendHeartbeat()` - Timer callback for heartbeat transmission
- `DoWork()` - Timer callback for work output
- `SendEncryptedMessage()` - Encrypts and sends messages to other clients
- `SimulateFailure()` - Stops heartbeats to simulate client failure

#### ClientCallback
Implements the callback interface for receiving server signals.

```csharp
[CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant)]
public class ClientCallback : IClientCallback
```

**Reentrant Design:**
- Allows callbacks during ongoing operations
- Prevents deadlocks in bidirectional communication
- Maintains reference to parent ClientApplication

#### CryptoManager
Handles AES-256 encryption for secure inter-client messaging.

```csharp
public class CryptoManager
```

**Security Features:**
- AES-256-CBC encryption
- Fixed key and IV (demo purposes - production would use key exchange)
- PKCS7 padding for variable-length messages
- UTF-8 encoding for text messages

**Methods:**
- `Encrypt(string plainText)` - Encrypts text to byte array
- `Decrypt(byte[] cipherText)` - Decrypts byte array to text

### 4.4 Data Contracts

#### ClientInfo
Data transfer object for client information.

```csharp
[DataContract]
public class ClientInfo
```

**Properties:**
- `ClientId` - Unique client identifier
- `Status` - Current operational status
- `LastHeartbeat` - Timestamp of last heartbeat
- `IsStandby` - Standby mode flag
- `RegisteredAt` - Registration timestamp

#### ClientStatus Enumeration
```csharp
public enum ClientStatus
{
    Standby,   // Client is in standby mode
    Dead,      // Client failed or disconnected
    Working    // Client is actively working
}
```

### 4.5 Main Launcher

#### Program Class
Application entry point with system launcher functionality.

```csharp
class Program
```

**Features:**
- Command-line argument processing
- Automatic system deployment
- Process management for multi-component launch
- Interactive menu system

**Launch Modes:**
- `server` - Starts server component only
- `client <id> <isStandby>` - Starts specific client
- No arguments - Launches complete system (1 server + 4 clients)

---

## 5. Database Schema

### 5.1 Tables

#### Clients Table
Stores current client registration and status information.

```sql
CREATE TABLE Clients (
    ClientId NVARCHAR(50) PRIMARY KEY,
    Status NVARCHAR(20),           -- Working/Standby/Dead
    LastHeartbeat DATETIME,
    RegisteredAt DATETIME,
    IsStandby BIT
)
```

#### ClientLogs Table
Audit trail of all system events and client actions.

```sql
CREATE TABLE ClientLogs (
    LogId INT IDENTITY(1,1) PRIMARY KEY,
    ClientId NVARCHAR(50),
    Event NVARCHAR(100),           -- Event type (e.g., CLIENT_REGISTERED)
    Timestamp DATETIME,
    Details NVARCHAR(MAX)          -- Additional event information
)
```

### 5.2 Key Operations
- **Upsert Pattern**: Insert new or update existing client records
- **Audit Logging**: All state changes recorded in ClientLogs
- **Parameterized Queries**: Protection against SQL injection
- **Connection Management**: Automatic open/close with using statements

---

## 6. Security Implementation

### 6.1 Encryption Details
- **Algorithm**: AES-256-CBC
- **Key Size**: 256 bits (32 bytes)
- **IV Size**: 128 bits (16 bytes)
- **Padding**: PKCS7
- **Text Encoding**: UTF-8

### 6.2 Security Considerations
- **Demo Environment**: Fixed keys for simplicity
- **Production Recommendations**:
  - Implement proper key exchange (Diffie-Hellman)
  - Use certificate-based authentication
  - Enable WCF security (Transport/Message level)
  - Implement proper key rotation

### 6.3 Communication Security
- **Current**: HTTP with no transport security
- **Production**: Should use HTTPS with certificates
- **Authentication**: Currently none - should implement client certificates

---

## 7. Configuration Guide

### 7.1 App.config Structure

```xml
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" />
  </startup>

  <system.serviceModel>
    <bindings>
      <wsDualHttpBinding>
        <binding name="DuplexBinding"
                 maxReceivedMessageSize="2147483647"
                 maxBufferPoolSize="2147483647">
          <security mode="None" />
        </binding>
      </wsDualHttpBinding>
    </bindings>

    <client>
      <endpoint name="FaultTolerantServiceEndpoint"
                address="http://localhost:8080/FaultTolerantService"
                binding="wsDualHttpBinding"
                bindingConfiguration="DuplexBinding"
                contract="FaultTolerantSystem.IFaultTolerantService" />
    </client>
  </system.serviceModel>

  <connectionStrings>
    <add name="FaultTolerantDB"
         connectionString="Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=FaultTolerantDB;Integrated Security=True"
         providerName="System.Data.SqlClient" />
  </connectionStrings>
</configuration>
```

### 7.2 Key Configuration Parameters

#### WCF Binding Configuration
- **Timeout Settings**: 1-10 minutes for various operations
- **Message Limits**: 2GB maximum for large encrypted payloads
- **Security Mode**: None (demo) - should be Message/Transport in production
- **Reader Quotas**: Maximum array and string lengths set to 2GB

#### Database Configuration
- **Provider**: SQL Server LocalDB
- **Security**: Integrated Windows Authentication
- **Database**: Auto-created on first run
- **Connection Pooling**: Default .NET connection pooling

#### Timing Configuration (Hardcoded)
- **Heartbeat Interval**: 10 seconds
- **Heartbeat Timeout**: 30 seconds
- **Work Output Interval**: 5 seconds
- **Heartbeat Check Interval**: 5 seconds

### 7.3 Customization Options

#### Changing Ports
Modify in both `App.config` and `ServerHost.cs`:
```csharp
// In ServerHost.cs
serviceHost.AddServiceEndpoint(
    typeof(IFaultTolerantService),
    binding,
    "http://localhost:9090/FaultTolerantService"); // Change port here
```

#### Adjusting Timeouts
Modify constants in `FaultTolerantService.cs`:
```csharp
private readonly int heartbeatTimeout = 45; // Change from 30 to 45 seconds
```

#### Database Connection
Modify connection string in `App.config` and `DatabaseManager.cs`.

---

## 8. User Guide

### 8.1 Starting the System

#### Automatic Launch 
```bash
# Navigate to output directory and run
FaultTolerantSystem.exe
```

This automatically starts:
- 1 Server
- 2 Working Clients (CLIENT1, CLIENT2)
- 2 Standby Clients (CLIENT3, CLIENT4)

#### Manual Component Launch
```bash
# Start server
FaultTolerantSystem.exe server

# Start working client
FaultTolerantSystem.exe client CLIENT1 false

# Start standby client
FaultTolerantSystem.exe client CLIENT3 true
```

### 8.2 Server Operations

**Server Interface:**
```
====================================================
         FAULT TOLERANT SYSTEM - SERVER
====================================================

✓ WCF Service started successfully
✓ Database initialized and ready
✓ Heartbeat monitoring active (30s timeout)

Commands:
  L - List all connected clients
  S - Simulate client failure
  Q - Quit server
```

**Key Commands:**
- **L**: Shows all clients with status and last heartbeat time
- **S**: Select a client to simulate failure for testing
- **Q**: Graceful server shutdown

### 8.3 Client Operations

**Working Client Display:**
```
[CLIENT1] WORKING... 14:32:15.123
[CLIENT1] Heartbeat #1 sent at 14:32:16
[CLIENT1] WORKING... 14:32:20.124
[CLIENT1] Heartbeat #2 sent at 14:32:26
```

**Client Commands:**
- **Q**: Quit client gracefully
- **F**: Simulate failure (stops heartbeats)
- **S**: Send encrypted message to another client
- **I**: Show client information and statistics

### 8.4 Testing Scenarios

#### Failover Testing
1. **Start the system** normally
2. **Press 'F'** in a working client window (CLIENT1 or CLIENT2)
3. **Wait 30+ seconds** for server to detect failure
4. **Observe** standby client automatically activates and begins working
5. **Verify** in server window that failover completed successfully

#### Message Testing
1. **Press 'S'** in any client window
2. **Enter target client ID** (e.g., CLIENT2)
3. **Type message** to send
4. **Observe** encrypted message appears in target client window

#### System Recovery Testing
1. **Close** a working client window (simulate crash)
2. **Wait** for server to detect failure
3. **Observe** failover to standby client
4. **Restart** failed client as standby
5. **Verify** system returns to normal operation

---

## 9. Development Guide

### 9.1 Building the Project

#### Prerequisites
- Visual Studio 2019 or later
- .NET Framework 4.7.2 SDK
- SQL Server LocalDB

#### Build Steps
```bash
# Clone or download the project
git clone <repository-url>

# Open in Visual Studio
# Or build from command line:
msbuild FaultTolerantSystem.sln /p:Configuration=Release
```

### 9.2 Debugging

#### Debugging Multiple Processes
1. **Set Startup Project** to FaultTolerantSystem
2. **Run without debugging** (Ctrl+F5) to launch all components
3. **Attach debugger** to specific processes as needed
4. **Set breakpoints** in relevant classes

#### Common Debug Scenarios
- **Client Registration**: Set breakpoints in `RegisterClient()`
- **Heartbeat Monitoring**: Debug `CheckHeartbeats()` timer
- **Failover Logic**: Trace `ActivateStandbyClient()` execution
- **Message Encryption**: Debug `CryptoManager` methods

---

## 10. Quick Reference

### 10.1 File Locations
- **Executable**: `bin\Debug\FaultTolerantSystem.exe`
- **Configuration**: `bin\Debug\FaultTolerantSystem.exe.config`
- **Database**: `%USERPROFILE%\` (auto-created by LocalDB)
- **Source Code**: `FaultTolerantSystem\**\*.cs`

### 10.2 Key Constants
```csharp
// Timing Constants
const int HEARTBEAT_INTERVAL = 10000;      // 10 seconds
const int HEARTBEAT_TIMEOUT = 30;          // 30 seconds
const int WORK_INTERVAL = 5000;            // 5 seconds
const int MONITOR_INTERVAL = 5000;         // 5 seconds

// Network Constants
const string SERVICE_URL = "http://localhost:8080/FaultTolerantService";
const string BINDING_NAME = "DuplexBinding";

// Encryption Constants
const int AES_KEY_SIZE = 256;              // bits
const int AES_BLOCK_SIZE = 128;            // bits
```

### 10.3 Service Endpoints
- **Primary Service**: `http://localhost:8080/FaultTolerantService`
- **Metadata**: `http://localhost:8080/FaultTolerantService/mex`
- **Database**: `(LocalDB)\MSSQLLocalDB`

### 10.4 Default Client Configuration
- **CLIENT1**: Working client (auto-start)
- **CLIENT2**: Working client (auto-start)
- **CLIENT3**: Standby client
- **CLIENT4**: Standby client

### 10.5 Troubleshooting Common Issues

#### "Port 8080 already in use"
- Check for other applications using port 8080
- Modify port in `App.config` and `ServerHost.cs`
- Use `netstat -an | findstr :8080` to identify conflicts

#### "Database connection failed"
- Ensure SQL Server LocalDB is installed
- Check Windows Authentication permissions
- Verify connection string in `App.config`

#### "Client cannot connect to server"
- Verify server is running and listening
- Check firewall settings for port 8080
- Ensure no network proxy interference

#### "Encryption/Decryption errors"
- Verify all clients use same CryptoManager configuration
- Check for corrupted message transmission
- Ensure proper character encoding (UTF-8)

### 10.6 Performance Metrics
- **Maximum Clients**: Limited by available memory (~1000 theoretical)
- **Failover Time**: < 1 second from detection to activation
- **Message Throughput**: ~100 messages/second per client
- **Database Operations**: ~500 operations/second
- **Memory Usage**: ~50MB base + ~1MB per client