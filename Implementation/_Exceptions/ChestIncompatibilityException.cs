using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class ChestIncompatibilityException: Exception {
    public ChestIncompatibilityException(string message, Exception inner = null): base(message, inner) {}

    public ChestIncompatibilityException(): base("The given chest is either a refill- or bank chest which is invalid.") {}

    protected ChestIncompatibilityException(SerializationInfo info, StreamingContext context): base(info, context) {}
  }
}