using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.LightsScenario2
{
    public interface ILightingRoomList
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> saveCommand;

        void saveConfirm(LightingRoomListBoolInputSigDelegate callback);
        void numberOfScenes(LightingRoomListUShortInputSigDelegate callback);
        void numberOfLoads(LightingRoomListUShortInputSigDelegate callback);
        void numberOfHouseScenes(LightingRoomListUShortInputSigDelegate callback);

    }

    public delegate void LightingRoomListBoolInputSigDelegate(BoolInputSig boolInputSig, ILightingRoomList lightingRoomList);
    public delegate void LightingRoomListUShortInputSigDelegate(UShortInputSig uShortInputSig, ILightingRoomList lightingRoomList);

    internal class LightingRoomList : ILightingRoomList, IDisposable
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
                public const uint saveCommand = 1;

                public const uint saveConfirm = 2;
            }
            internal static class Numerics
            {

                public const uint numberOfScenes = 1;
                public const uint numberOfLoads = 2;
                public const uint numberOfHouseScenes = 3;
            }
        }

        #endregion

        #region Construction and Initialization

        internal LightingRoomList(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.saveCommand, onsaveCommand);

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

        public event EventHandler<UIEventArgs> saveCommand;
        private void onsaveCommand(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = saveCommand;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void saveConfirm(LightingRoomListBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.saveConfirm], this);
            }
        }


        public void numberOfScenes(LightingRoomListUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.numberOfScenes], this);
            }
        }

        public void numberOfLoads(LightingRoomListUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.numberOfLoads], this);
            }
        }

        public void numberOfHouseScenes(LightingRoomListUShortInputSigDelegate callback)
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "LightingRoomList", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
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
