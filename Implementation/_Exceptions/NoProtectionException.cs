using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;
using System.Security;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class NoProtectionException: Exception {
    public DPoint TileLocation { get; private set; }


    public NoProtectionException(string message, DPoint tileLocation = default(DPoint)): base(message, null) {
      this.TileLocation = tileLocation;
    }

    public NoProtectionException(DPoint tileLocation): base("A block or object was expected to be protected.") {
      this.TileLocation = tileLocation;
    }

    public NoProtectionException(string message, Exception inner = null): base(message, inner) {}

    public NoProtectionException() : base("A block or object was expected to be protected.") {}

    #region [Serializable Implementation]
    protected NoProtectionException(SerializationInfo info, StreamingContext context): base(info, context) {
      this.TileLocation = (DPoint)info.GetValue("NoProtectionException_TileLocation", typeof(DPoint));
    }

    [SecurityCritical]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
      base.GetObjectData(info, context);

      info.AddValue("NoProtectionException_TileLocation", this.TileLocation, typeof(DPoint));
    }
    #endregion
  }
}