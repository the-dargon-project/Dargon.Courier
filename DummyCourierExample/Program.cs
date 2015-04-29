﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dargon.Courier.Identities;
using Dargon.Courier.Messaging;
using Dargon.Courier.Networking;
using Dargon.Courier.Peering;
using Dargon.Courier.PortableObjects;
using Dargon.PortableObjects;
using ItzWarty;
using ItzWarty.Threading;

namespace DummyCourierExample {
   public static class Program {
      private static void Main(string[] args) {
         var network = new LocalCourierNetwork(dropRate: 0.1);
         var tasks = Util.Generate(4, i => new Thread(() => EntryPoint(i, network)).With(t => t.Start()));
         tasks.ForEach(t => t.Join());
      }

      public static void EntryPoint(int i, CourierNetwork network) {
         IThreadingFactory threadingFactory = new ThreadingFactory();
         ISynchronizationFactory synchronizationFactory = new SynchronizationFactory();
         IThreadingProxy threadingProxy = new ThreadingProxy(threadingFactory, synchronizationFactory);
         GuidProxy guidProxy = new GuidProxyImpl();
         IPofContext courierPofContext = new DargonCourierImplPofContext(1337);
         IPofSerializer courierSerializer = new PofSerializer(courierPofContext);
         Guid localIdentifier = guidProxy.NewGuid();
         var endpoint = new CourierEndpointImpl(courierSerializer, localIdentifier, "node" + i);
         var networkContext = network.Join(endpoint);

         var networkBroadcaster = new NetworkBroadcasterImpl(endpoint, networkContext, courierSerializer);
         var unacknowledgedReliableMessageContainer = new UnacknowledgedReliableMessageContainer();
         var messageTransmitter = new MessageTransmitterImpl(guidProxy, courierSerializer, networkBroadcaster, unacknowledgedReliableMessageContainer);
         var messageSender = new MessageSenderImpl(guidProxy, unacknowledgedReliableMessageContainer, messageTransmitter);
         var messageAcknowledger = new MessageAcknowledgerImpl(networkBroadcaster, unacknowledgedReliableMessageContainer);
         var periodicAnnouncer = new PeriodicAnnouncerImpl(threadingProxy, courierSerializer, endpoint, networkBroadcaster);
         periodicAnnouncer.Start();
         var periodicResender = new PeriodicResenderImpl(threadingProxy, unacknowledgedReliableMessageContainer, messageTransmitter);
         periodicResender.Start();

         ReceivedMessageFactory receivedMessageFactory = new ReceivedMessageFactoryImpl(courierSerializer);
         MessageRouter messageRouter = new MessageRouterImpl(receivedMessageFactory);
         var peerRegistry = new PeerRegistryImpl(courierSerializer);
         var networkReceiver = new NetworkReceiverImpl(endpoint, networkContext, courierSerializer, messageRouter, messageAcknowledger, peerRegistry);
         networkReceiver.Initialize();

         messageRouter.RegisterPayloadHandler<string>(m => {
//            Console.WriteLine(i + ": " + m.Payload);
         });

         Thread.Sleep(3000);

         if (i == 0) {
            while (true) {
               for (var j = 0; j < 50; j++) {
                  Console.WriteLine(unacknowledgedReliableMessageContainer.GetUnsentMessagesRemaining() + " pending");
                  for (var k = 0; k < 10000; k++) {
                     foreach (var peer in peerRegistry.EnumeratePeers()) {
                        messageSender.SendReliableUnicast(peer.Id, "Message " + j + " hello from " + i + ", " + peer.Id, MessagePriority.Low);
                     }
                  }
                  Thread.Sleep(1);
               }

               for (var j = 0; j < 1000; j++) {
                  Console.WriteLine(unacknowledgedReliableMessageContainer.GetUnsentMessagesRemaining() + " pending");
                  Thread.Sleep(1);
               }
            }
         }
      }
   }
}