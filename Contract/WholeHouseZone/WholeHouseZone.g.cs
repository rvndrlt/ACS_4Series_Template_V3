using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.WholeHouseZone
{
    public interface IWholeHouseZone
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> SelectWholeHouseZone;

        void WholeHouseZoneSelected(WholeHouseZoneBoolInputSigDelegate callback);
        void HouseZoneName(WholeHouseZoneStringInputSigDelegate callback);
        void HouseZoneIcon(WholeHouseZoneStringInputSigDelegate callback);
        void HouseZoneStatus(WholeHouseZoneStringInputSigDelegate callback);

    }

    public delegate void WholeHouseZoneBoolInputSigDelegate(BoolInputSig boolInputSig, IWholeHouseZone wholeHouseZone);
    public delegate void WholeHouseZoneStringInputSigDelegate(StringInputSig stringInputSig, IWholeHouseZone wholeHouseZone);

    internal class WholeHouseZone : IWholeHouseZone, IDisposable
    {
        #region Standard CH5 Component members

        private ComponentMediator ComponentMediator { get; set; }

        public object UserObject { get; set; }

        public uint ControlJoinId { get; private set; }

        private IList<BasicTriListWithSmartObject> _devices;
        public IList<BasicTriListWithSmartObject> Devices { get { return _devices; } }

        #endregion

        #region Joins

        private static class Joins
        {
            internal static class Booleans
            {
                public const uint SelectWholeHouseZone = 1;

                public const uint WholeHouseZoneSelected = 1;
            }
            internal static class Strings
            {

                public const uint HouseZoneName = 1;
                public const uint HouseZoneIcon = 2;
                public const uint HouseZoneStatus = 3;
            }
        }

        #endregion

        #region Construction and Initialization

        internal WholeHouseZone(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.SelectWholeHouseZone, onSelectWholeHouseZone);

        }

        public void AddDevice(BasicTriListWithSmartObject device)
        {
            Devices.Add(device);
            ComponentMediator.HookSmartObjectEvents(device.SmartObjects[ControlJoinId]);
        }

        public void RemoveDevice(BasicTriListWithSmartObject device)
        {
            Devices.Remove(device);
            ComponentMediator.UnHookSmartObjectEvents(device.SmartObjects[ControlJoinId]);
        }

        #endregion

        #region CH5 Contract

        public event EventHandler<UIEventArgs> SelectWholeHouseZone;
        private void onSelectWholeHouseZone(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = SelectWholeHouseZone;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void WholeHouseZoneSelected(WholeHouseZoneBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.WholeHouseZoneSelected], this);
            }
        }


        public void HouseZoneName(WholeHouseZoneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.HouseZoneName], this);
            }
        }

        public void HouseZoneIcon(WholeHouseZoneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.HouseZoneIcon], this);
            }
        }

        public void HouseZoneStatus(WholeHouseZoneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.HouseZoneStatus], this);
            }
        }

        #endregion

        #region Overrides

        public override int GetHashCode()
        {
            return (int)ControlJoinId;
        }

        public override string ToString()
        {
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "WholeHouseZone", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            SelectWholeHouseZone = null;
        }

        #endregion

    }
}
