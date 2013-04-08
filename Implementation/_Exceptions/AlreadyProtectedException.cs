using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class AlreadyProtectedException: Exception {
    public AlreadyProtectedException(string message, Exception inner = null): base(message, inner) {}

    public AlreadyProtectedException(): base("The block or object is already protected.") {}

    protected AlreadyProtectedException(SerializationInfo info, StreamingContext context): base(info, context) {}
  }
}