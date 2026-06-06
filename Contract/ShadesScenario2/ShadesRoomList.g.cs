using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.ShadesScenario2
{
    public interface IShadesRoomList
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> saveCommand;

        void saveConfirm(ShadesRoomListBoolInputSigDelegate callback);
        void numberOfScenes(ShadesRoomListUShortInputSigDelegate callback);
        void numberOfShades(ShadesRoomListUShortInputSigDelegate callback);
        void numberOfHouseScenes(ShadesRoomListUShortInputSigDelegate callback);

    }

    public delegate void ShadesRoomListBoolInputSigDelegate(BoolInputSig boolInputSig, IShadesRoomList shadesRoomList);
    public delegate void ShadesRoomListUShortInputSigDelegate(UShortInputSig uShortInputSig, IShadesRoomList shadesRoomList);

    internal class ShadesRoomList : IShadesRoomList, IDisposable
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

                public const uint saveConfirm = 1;
            }
            internal static class Numerics
            {
                public const uint saveCommand = 4;

                public const uint numberOfScenes = 1;
                public const uint numberOfShades = 2;
                public const uint numberOfHouseScenes = 3;
            }
        }

        #endregion

        #region Construction and Initialization

        internal ShadesRoomList(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureNumericEvent(controlJoinId, Joins.Numerics.saveCommand, onsaveCommand);

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


        public void saveConfirm(ShadesRoomListBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.saveConfirm], this);
            }
        }

        public event EventHandler<UIEventArgs> saveCommand;
        private void onsaveCommand(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = saveCommand;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void numberOfScenes(ShadesRoomListUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.numberOfScenes], this);
            }
        }

        public void numberOfShades(ShadesRoomListUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.numberOfShades], this);
            }
        }

        public void numberOfHouseScenes(ShadesRoomListUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.numberOfHouseScenes], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "ShadesRoomList", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            saveCommand = null;
        }

        #endregion

    }
}
