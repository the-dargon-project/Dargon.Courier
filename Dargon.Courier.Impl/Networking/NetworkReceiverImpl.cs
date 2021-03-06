﻿using System;
using System.IO;
using System.Net;
using Dargon.Courier.Identities;
using Dargon.Courier.Messaging;
using Dargon.Courier.Peering;
using Dargon.Courier.PortableObjects;
using Dargon.PortableObjects;
using ItzWarty;

namespace Dargon.Courier.Networking {
   public class NetworkReceiverImpl {
      private readonly ReadableCourierEndpoint localEndpoint;
      private readonly CourierNetworkContext networkContext;
      private readonly IPofSerializer pofSerializer;
      private readonly MessageRouter messageRouter;
      private readonly MessageAcknowledger messageAcknowledger;
      private readonly PeerRegistryImpl peerRegistry;
      private readonly ReceivedMessageFactory receivedMessageFactory;

      public NetworkReceiverImpl(ReadableCourierEndpoint localEndpoint, CourierNetworkContext networkContext, IPofSerializer pofSerializer, MessageRouter messageRouter, MessageAcknowledger messageAcknowledger, PeerRegistryImpl peerRegistry, ReceivedMessageFactory receivedMessageFactory) {
         this.localEndpoint = localEndpoint;
         this.networkContext = networkContext;
         this.pofSerializer = pofSerializer;
         this.messageRouter = messageRouter;
         this.messageAcknowledger = messageAcknowledger;
         this.peerRegistry = peerRegistry;
         this.receivedMessageFactory = receivedMessageFactory;
      }

      public void Initialize() {
         networkContext.DataArrived += HandleDataArrived;
      }

      private void HandleDataArrived(CourierNetwork network, byte[] data, int offset, int length, IPEndPoint remoteEndPoint) {
         using (var ms = new MemoryStream(data, offset, length))
         using (var reader = new BinaryReader(ms)) {
            ulong magic = reader.ReadUInt64();
            if (magic != NetworkingConstants.kMessageHeader) {
               return;
            }

            Guid senderId = reader.ReadGuid();
            if (localEndpoint.Matches(senderId)) {
               return;
            }

            var payload = pofSerializer.Deserialize(reader);
            if (payload is CourierMessageV1) {
               HandleInboundMessage(senderId, (CourierMessageV1)payload, remoteEndPoint);
            } else if (payload is CourierAnnounceV1) {
               HandleInboundAnnounce(senderId, (CourierAnnounceV1)payload, remoteEndPoint);
            } else if (payload is CourierMessageAcknowledgeV1) {
               HandleCourierAcknowledgement(senderId, (CourierMessageAcknowledgeV1)payload);
            }
         }
      }

      private void HandleInboundMessage(Guid senderId, CourierMessageV1 message, IPEndPoint remoteEndPoint) {
         if (!localEndpoint.Matches(message.RecipientId)) {
            return;
         }
         if (message.MessageFlags.HasFlag(MessageFlags.AcknowledgementRequired)) {
            messageAcknowledger.SendAcknowledge(senderId, message.Id);
         }
         var receivedMessage = receivedMessageFactory.CreateReceivedMessage(senderId, message, remoteEndPoint.Address);
         var payloadType = receivedMessage.Payload.GetType();
         messageRouter.RouteMessage(payloadType, receivedMessage);
      }

      private void HandleInboundAnnounce(Guid senderId, CourierAnnounceV1 payload, IPEndPoint remoteEndPoint) {
         peerRegistry.HandlePeerAnnounce(senderId, payload, remoteEndPoint);
      }

      private void HandleCourierAcknowledgement(Guid senderId, CourierMessageAcknowledgeV1 ack) {
         if (!localEndpoint.Matches(ack.RecipientId)) {
            return;
         }
         messageAcknowledger.HandleAcknowledge(senderId, ack.MessageId);
//         Console.WriteLine("Handling acknowledge from " + senderId + " for message " + ack.MessageId);
      }
   }
}
