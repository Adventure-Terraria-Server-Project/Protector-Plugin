using System;
using System.Runtime.Serialization;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class BankChestAlreadyInstancedException : Exception {
    public BankChestAlreadyInstancedException(string message, Exception inner = null) : base(message, inner) {}

    public BankChestAlreadyInstancedException() : base("There is already an instance of the bank chest on this world.") {}

    protected BankChestAlreadyInstancedException(SerializationInfo info, StreamingContext context) : base(info, context) {}
  }
}