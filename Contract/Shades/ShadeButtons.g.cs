using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.Shades
{
    public interface IShadeButtons
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> ShadeOpen;
        event EventHandler<UIEventArgs> ShadeClose;
        event EventHandler<UIEventArgs> ShadeStop;

        void ShadeOpened(ShadeButtonsBoolInputSigDelegate callback);
        void ShadeStopped(ShadeButtonsBoolInputSigDelegate callback);
        void ShadeClosed(ShadeButtonsBoolInputSigDelegate callback);
        void ShadeName(ShadeButtonsStringInputSigDelegate callback);

    }

    public delegate void ShadeButtonsBoolInputSigDelegate(BoolInputSig boolInputSig, IShadeButtons shadeButtons);
    public delegate void ShadeButtonsStringInputSigDelegate(StringInputSig stringInputSig, IShadeButtons shadeButtons);

    internal class ShadeButtons : IShadeButtons, IDisposable
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
                public const uint ShadeOpen = 1;
                public const uint ShadeClose = 2;
                public const uint ShadeStop = 3;

                public const uint ShadeOpened = 1;
                public const uint ShadeStopped = 2;
                public const uint ShadeClosed = 3;
            }
            internal static class Strings
            {

                public const uint ShadeName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal ShadeButtons(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.ShadeOpen, onShadeOpen);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.ShadeClose, onShadeClose);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.ShadeStop, onShadeStop);

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

        public event EventHandler<UIEventArgs> ShadeOpen;
        private void onShadeOpen(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = ShadeOpen;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> ShadeClose;
        private void onShadeClose(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = ShadeClose;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> ShadeStop;
        private void onShadeStop(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = ShadeStop;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void ShadeOpened(ShadeButtonsBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.ShadeOpened], this);
            }
        }

        public void ShadeStopped(ShadeButtonsBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.ShadeStopped], this);
            }
        }

        public void ShadeClosed(ShadeButtonsBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.ShadeClosed], this);
            }
        }


        public void ShadeName(ShadeButtonsStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.ShadeName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "ShadeButtons", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            ShadeOpen = null;
            ShadeClose = null;
            ShadeStop = null;
        }

        #endregion

    }
}
