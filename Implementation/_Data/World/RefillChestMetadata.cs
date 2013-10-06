using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

using Terraria.Plugins.Common;

using TShockAPI;
using Newtonsoft.Json;

namespace Terraria.Plugins.CoderCow.Protector {
  public class RefillChestMetadata {
    public int Owner { get; set; }
    public ItemData[] RefillItems { get; set; }

    #region [Property: RefillTimer, RefillStartTime, RefillTime]
    private Timer refillTimer;

    [JsonIgnore]
    public Timer RefillTimer {
      get { return this.refillTimer; }
      set {
        Contract.Requires<ArgumentNullException>(value != null);
        this.refillTimer = value;
      }
    }

    public DateTime RefillStartTime {
      get { return this.RefillTimer.StartTime; }
      set { this.RefillTimer.StartTime = value; }
    }

    public TimeSpan RefillTime {
      get { return this.RefillTimer.TimeSpan; }
    }
    #endregion

    public bool OneLootPerPlayer { get; set; }
    public int RemainingLoots { get; set; }
    public Collection<int> Looters { get; set; }
    public bool AutoLock { get; set; }


    public RefillChestMetadata(int owner): base() {
      this.Owner = owner;
      this.RefillItems = new ItemData[Chest.maxItems];
      this.refillTimer = new Timer(TimeSpan.Zero, null, null);
      this.RemainingLoots = -1;
    }
  }
}
