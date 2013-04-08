using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class ProtectionAlreadySharedException: Exception {
    public ProtectionAlreadySharedException(string message, Exception inner = null): base(message, inner) {}

    public ProtectionAlreadySharedException(): base("A protection is already shared with a user or group.") {}

    protected ProtectionAlreadySharedException(SerializationInfo info, StreamingContext context): base(info, context) {}
  }
}