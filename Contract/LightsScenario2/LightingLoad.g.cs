using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.LightsScenario2
{
    public interface ILightingLoad
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> loadOn;
        event EventHandler<UIEventArgs> loadOff;
        event EventHandler<UIEventArgs> setLoadLevel;

        void loadIsOn(LightingLoadBoolInputSigDelegate callback);
        void loadLevel(LightingLoadUShortInputSigDelegate callback);
        void loadName(LightingLoadStringInputSigDelegate callback);

    }

    public delegate void LightingLoadBoolInputSigDelegate(BoolInputSig boolInputSig, ILightingLoad lightingLoad);
    public delegate void LightingLoadUShortInputSigDelegate(UShortInputSig uShortInputSig, ILightingLoad lightingLoad);
    public delegate void LightingLoadStringInputSigDelegate(StringInputSig stringInputSig, ILightingLoad lightingLoad);

    internal class LightingLoad : ILightingLoad, IDisposable
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
                public const uint loadOn = 1;
                public const uint loadOff = 2;

                public const uint loadIsOn = 1;
            }
            internal static class Numerics
            {
                public const uint setLoadLevel = 1;

                public const uint loadLevel = 1;
            }
            internal static class Strings
            {

                public const uint loadName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal LightingLoad(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.loadOn, onloadOn);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.loadOff, onloadOff);
            ComponentMediator.ConfigureNumericEvent(controlJoinId, Joins.Numerics.setLoadLevel, onsetLoadLevel);

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

        public event EventHandler<UIEventArgs> loadOn;
        private void onloadOn(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = loadOn;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> loadOff;
        private void onloadOff(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = loadOff;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void loadIsOn(LightingLoadBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.loadIsOn], this);
            }
        }

        public event EventHandler<UIEventArgs> setLoadLevel;
        private void onsetLoadLevel(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = setLoadLevel;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void loadLevel(LightingLoadUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.loadLevel], this);
            }
        }


        public void loadName(LightingLoadStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.loadName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "LightingLoad", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            loadOn = null;
            loadOff = null;
            setLoadLevel = null;
        }

        #endregion

    }
}
