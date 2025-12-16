using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.Floors
{
    public interface IFloorSelect
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> SelectFloor;

        void FloorIsSelected(FloorSelectBoolInputSigDelegate callback);
        void FloorName(FloorSelectStringInputSigDelegate callback);

    }

    public delegate void FloorSelectBoolInputSigDelegate(BoolInputSig boolInputSig, IFloorSelect floorSelect);
    public delegate void FloorSelectStringInputSigDelegate(StringInputSig stringInputSig, IFloorSelect floorSelect);

    internal class FloorSelect : IFloorSelect, IDisposable
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
                public const uint SelectFloor = 1;

                public const uint FloorIsSelected = 1;
            }
            internal static class Strings
            {
                public const uint FloorName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal FloorSelect(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.SelectFloor, onSelectFloor);

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

        public event EventHandler<UIEventArgs> SelectFloor;
        private void onSelectFloor(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = SelectFloor;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void FloorIsSelected(FloorSelectBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.FloorIsSelected], this);
            }
        }

        public void FloorName(FloorSelectStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.FloorName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "FloorSelect", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            SelectFloor = null;
        }

        #endregion

    }
}
