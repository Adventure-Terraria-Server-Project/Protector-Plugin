using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;
using System.Security;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class NoProtectionException: Exception {
    #region [Property: TileLocation]
    private readonly DPoint tileLocation;

    public DPoint TileLocation {
      get { return this.tileLocation; }
    }
    #endregion


    #region [Method: Constructor]
    public NoProtectionException(string message, DPoint tileLocation = default(DPoint)): base(message, null) {
      this.tileLocation = tileLocation;
    }

    public NoProtectionException(DPoint tileLocation): base("A block or object was expected to be protected.") {
      this.tileLocation = tileLocation;
    }

    public NoProtectionException(string message, Exception inner = null): base(message, inner) {}

    public NoProtectionException() : base("A block or object was expected to be protected.") {}
    #endregion

    #region [Serializable Implementation]
    protected NoProtectionException(SerializationInfo info, StreamingContext context): base(info, context) {
      this.tileLocation = (DPoint)info.GetValue("NoProtectionException_TileLocation", typeof(DPoint));
    }

    [SecurityCritical]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
      base.GetObjectData(info, context);

      info.AddValue("NoProtectionException_TileLocation", this.tileLocation, typeof(DPoint));
    }
    #endregion
  }
}