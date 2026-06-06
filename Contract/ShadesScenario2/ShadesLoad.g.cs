using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.ShadesScenario2
{
    public interface IShadesLoad
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> shadeOpen;
        event EventHandler<UIEventArgs> shadeStop;
        event EventHandler<UIEventArgs> shadeClose;
        event EventHandler<UIEventArgs> setShadeLevel;

        void shadeIsOpen(ShadesLoadBoolInputSigDelegate callback);
        void shadeIsStopped(ShadesLoadBoolInputSigDelegate callback);
        void shadeIsClosed(ShadesLoadBoolInputSigDelegate callback);
        void shadeLevel(ShadesLoadUShortInputSigDelegate callback);
        void shadeName(ShadesLoadStringInputSigDelegate callback);

    }

    public delegate void ShadesLoadBoolInputSigDelegate(BoolInputSig boolInputSig, IShadesLoad shadesLoad);
    public delegate void ShadesLoadUShortInputSigDelegate(UShortInputSig uShortInputSig, IShadesLoad shadesLoad);
    public delegate void ShadesLoadStringInputSigDelegate(StringInputSig stringInputSig, IShadesLoad shadesLoad);

    internal class ShadesLoad : IShadesLoad, IDisposable
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
                public const uint shadeOpen = 1;
                public const uint shadeStop = 2;
                public const uint shadeClose = 3;

                public const uint shadeIsOpen = 1;
                public const uint shadeIsStopped = 2;
                public const uint shadeIsClosed = 3;
            }
            internal static class Numerics
            {
                public const uint setShadeLevel = 1;

                public const uint shadeLevel = 1;
            }
            internal static class Strings
            {

                public const uint shadeName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal ShadesLoad(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.shadeOpen, onshadeOpen);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.shadeStop, onshadeStop);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.shadeClose, onshadeClose);
            ComponentMediator.ConfigureNumericEvent(controlJoinId, Joins.Numerics.setShadeLevel, onsetShadeLevel);

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

        public event EventHandler<UIEventArgs> shadeOpen;
        private void onshadeOpen(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = shadeOpen;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> shadeStop;
        private void onshadeStop(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = shadeStop;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> shadeClose;
        private void onshadeClose(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = shadeClose;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void shadeIsOpen(ShadesLoadBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.shadeIsOpen], this);
            }
        }

        public void shadeIsStopped(ShadesLoadBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.shadeIsStopped], this);
            }
        }

        public void shadeIsClosed(ShadesLoadBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.shadeIsClosed], this);
            }
        }

        public event EventHandler<UIEventArgs> setShadeLevel;
        private void onsetShadeLevel(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = setShadeLevel;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void shadeLevel(ShadesLoadUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.shadeLevel], this);
            }
        }


        public void shadeName(ShadesLoadStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.shadeName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "ShadesLoad", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            shadeOpen = null;
            shadeStop = null;
            shadeClose = null;
            setShadeLevel = null;
        }

        #endregion

    }
}
