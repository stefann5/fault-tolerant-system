# Fault Tolerant System - Documentation

## Table of Contents
1. [Project Overview](#1-project-overview)
2. [System Architecture](#2-system-architecture)
3. [User Guide](#3-user-guide)
4. [Quick Reference](#4-quick-reference)

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

---

## 2. System Architecture

### 2.1 Overview
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

### 2.2 Core Components
- **Server**: WCF service managing clients, heartbeats, and failover
- **Working Clients**: Output "WORKING..." every 5 seconds
- **Standby Clients**: Ready for activation when working clients fail
- **Database**: Stores client status and event logs

### 2.3 Fault Tolerance Process
1. **Normal Operation**: Working clients send heartbeats every 10 seconds
2. **Failure Detection**: Server marks clients dead after 30 seconds without heartbeat
3. **Automatic Failover**: Server activates oldest standby client
4. **Recovery**: New working client begins operation within 1 second


## 3. User Guide

### 3.1 Starting the System

#### Automatic Launch 
```bash
# Navigate to output directory and run
FaultTolerantSystem.exe
```

This automatically starts:
- 1 Server
- 2 Working Clients (CLIENT1, CLIENT2)
- 2 Standby Clients (CLIENT3, CLIENT4)

### 3.2 Server Operations

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

### 3.3 Client Operations

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

### 3.4 Testing Failover

1. **Start the system** normally
2. **Press 'F'** in a working client window (CLIENT1 or CLIENT2)
3. **Wait 30+ seconds** for server to detect failure
4. **Observe** standby client automatically activates and begins working
5. **Verify** in server window that failover completed successfully

### 3.5 Testing Encrypted Messaging

1. **Press 'S'** in any client window
2. **Enter target client ID** (e.g., CLIENT2)
3. **Type message** to send
4. **Observe** encrypted message appears in target client window


## 4. Quick Reference

### File Locations
- **Executable**: `bin\Debug\FaultTolerantSystem.exe`
- **Configuration**: `bin\Debug\FaultTolerantSystem.exe.config`
- **Database**: `%USERPROFILE%\` (auto-created by LocalDB)

### Key Ports
- **8080**: WCF Service (configurable)
- **1433**: SQL Server LocalDB (default)

### Default Timings
- **Heartbeat**: Every 10 seconds
- **Work Output**: Every 5 seconds  
- **Timeout Detection**: 30 seconds
- **Failover Time**: < 1 second

### Project Structure
```
FaultTolerantSystem/
├── Client/ClientApplication.cs      # Client logic
├── Server/FaultTolerantService.cs   # Core service
├── Server/ServerHost.cs             # WCF hosting
├── Server/DatabaseManager.cs       # Database operations
├── Common/IFaultTolerantService.cs  # WCF contracts
├── MainLauncher.cs                  # Entry point
└── App.config                       # Configuration
```