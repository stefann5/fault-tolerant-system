using System;
using System.ServiceModel;
using System.Runtime.Serialization;

namespace FaultTolerantSystem
{
    // Service Contract
    [ServiceContract(CallbackContract = typeof(IClientCallback))]
    public interface IFaultTolerantService
    {
        [OperationContract]
        string RegisterClient(string clientId, bool isStandby);

        [OperationContract]
        void SendHeartbeat(string clientId);

        [OperationContract]
        void UnregisterClient(string clientId);

        [OperationContract]
        byte[] GetEncryptedMessage(string senderId, string receiverId, byte[] encryptedData);

        [OperationContract]
        ClientInfo[] GetAllClients();

        [OperationContract]
        void SimulateFailure(string clientId);
    }

    // Callback Contract for Server to Client communication
    [ServiceContract]
    public interface IClientCallback
    {
        [OperationContract(IsOneWay = true)]
        void StartWorking();

        [OperationContract(IsOneWay = true)]
        void StopWorking();

        [OperationContract(IsOneWay = true)]
        void ReceiveMessage(byte[] encryptedData, string senderId);
    }

    // Data Contracts
    [DataContract]
    public class ClientInfo
    {
        [DataMember]
        public string ClientId { get; set; }

        [DataMember]
        public ClientStatus Status { get; set; }

        [DataMember]
        public DateTime LastHeartbeat { get; set; }

        [DataMember]
        public bool IsStandby { get; set; }

        [DataMember]
        public DateTime RegisteredAt { get; set; }
    }

    public enum ClientStatus
    {
        Standby,
        Dead,
        Working
    }
}