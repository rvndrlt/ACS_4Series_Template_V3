using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.Lights
{
    public interface ILightButton
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> LightButtonSelect;

        void LightButtonSelected(LightButtonBoolInputSigDelegate callback);
        void LightButtonName(LightButtonStringInputSigDelegate callback);

    }

    public delegate void LightButtonBoolInputSigDelegate(BoolInputSig boolInputSig, ILightButton lightButton);
    public delegate void LightButtonStringInputSigDelegate(StringInputSig stringInputSig, ILightButton lightButton);

    internal class LightButton : ILightButton, IDisposable
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
                public const uint LightButtonSelect = 1;

                public const uint LightButtonSelected = 1;
            }
            internal static class Strings
            {

                public const uint LightButtonName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal LightButton(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.LightButtonSelect, onLightButtonSelect);

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

        public event EventHandler<UIEventArgs> LightButtonSelect;
        private void onLightButtonSelect(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = LightButtonSelect;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void LightButtonSelected(LightButtonBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.LightButtonSelected], this);
            }
        }


        public void LightButtonName(LightButtonStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.LightButtonName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "LightButton", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            LightButtonSelect = null;
        }

        #endregion

    }
}
