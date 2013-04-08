using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class ProtectionNotSharedException: Exception {
    public ProtectionNotSharedException(string message, Exception inner = null): base(message, inner) {}

    public ProtectionNotSharedException(): base("A protection is not shared with the given user or group.") {}

    protected ProtectionNotSharedException(SerializationInfo info, StreamingContext context): base(info, context) {}
  }
}