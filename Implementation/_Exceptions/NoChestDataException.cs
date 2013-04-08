using System;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;
using System.Security;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  [Serializable]
  public class NoChestDataException: Exception {
    #region [Property: ChestLocation]
    private readonly DPoint chestLocation;

    public DPoint ChestLocation {
      get { return this.chestLocation; }
    }
    #endregion


    #region [Method: Constructor]
    public NoChestDataException(string message, DPoint chestLocation = default(DPoint)): base(message, null) {
      this.chestLocation = chestLocation;
    }

    public NoChestDataException(DPoint chestLocation): base("There are no chest data registered with this chest.") {
      this.chestLocation = chestLocation;
    }

    public NoChestDataException(string message, Exception inner = null): base(message, inner) {}

    public NoChestDataException(): base("There are no chest data registered with this chest.") {}
    #endregion

    #region [Serializable Implementation]
    protected NoChestDataException(SerializationInfo info, StreamingContext context) : base(info, context) {
      this.chestLocation = (DPoint)info.GetValue("NoChestDataException_ChestLocation", typeof(DPoint));
    }

    [SecurityCritical]
    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
      base.GetObjectData(info, context);

      info.AddValue("NoChestDataException_ChestLocation", this.chestLocation, typeof(DPoint));
    }
    #endregion
  }
}