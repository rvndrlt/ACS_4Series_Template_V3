using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.RoomSelect
{
    public interface IRoom
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> selectZone;

        void zoneIsSelected(RoomBoolInputSigDelegate callback);
        void zoneName(RoomStringInputSigDelegate callback);
        void zoneStatus1(RoomStringInputSigDelegate callback);
        void zoneStatus2(RoomStringInputSigDelegate callback);
        void zoneImage(RoomStringInputSigDelegate callback);

    }

    public delegate void RoomBoolInputSigDelegate(BoolInputSig boolInputSig, IRoom room);
    public delegate void RoomStringInputSigDelegate(StringInputSig stringInputSig, IRoom room);

    internal class Room : IRoom, IDisposable
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
                public const uint selectZone = 1;

                public const uint zoneIsSelected = 1;
            }
            internal static class Strings
            {

                public const uint zoneName = 1;
                public const uint zoneStatus1 = 2;
                public const uint zoneStatus2 = 3;
                public const uint zoneImage = 4;
            }
        }

        #endregion

        #region Construction and Initialization

        internal Room(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.selectZone, onselectZone);

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

        public event EventHandler<UIEventArgs> selectZone;
        private void onselectZone(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = selectZone;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void zoneIsSelected(RoomBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.zoneIsSelected], this);
            }
        }


        public void zoneName(RoomStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.zoneName], this);
            }
        }

        public void zoneStatus1(RoomStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.zoneStatus1], this);
            }
        }

        public void zoneStatus2(RoomStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.zoneStatus2], this);
            }
        }

        public void zoneImage(RoomStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.zoneImage], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "Room", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            selectZone = null;
        }

        #endregion

    }
}
