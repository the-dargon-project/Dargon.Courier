﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.Courier.Messaging;
using Dargon.Courier.PortableObjects;
using Dargon.Ryu;

namespace Dargon.Courier {
   public class CourierImplRyuPackage : RyuPackageV1 {
      public CourierImplRyuPackage() {
         Instance<ReceivedMessageFactory, ReceivedMessageFactoryImpl>();
         Instance<CourierClientFactory, CourierClientFactoryImpl>();

         PofContext<DargonCourierImplPofContext>();
      }
   }
}
