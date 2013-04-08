using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class ChestTypeAlreadyDefinedException: Exception {
    public ChestTypeAlreadyDefinedException(string message, Exception inner = null) : base(message, inner) {}

    public ChestTypeAlreadyDefinedException() : base("The chest is already defined as this type of chest.") {}

    protected ChestTypeAlreadyDefinedException(SerializationInfo info, StreamingContext context) : base(info, context) {}
  }
}