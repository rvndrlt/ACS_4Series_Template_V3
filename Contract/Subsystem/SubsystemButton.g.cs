using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.Subsystem
{
    public interface ISubsystemButton
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> SelectSubsystem;

        void SubsystemSelected(SubsystemButtonBoolInputSigDelegate callback);
        void SubsystemName(SubsystemButtonStringInputSigDelegate callback);
        void SubsystemIcon(SubsystemButtonStringInputSigDelegate callback);
        void SubsystemStatus(SubsystemButtonStringInputSigDelegate callback);

    }

    public delegate void SubsystemButtonBoolInputSigDelegate(BoolInputSig boolInputSig, ISubsystemButton subsystemButton);
    public delegate void SubsystemButtonStringInputSigDelegate(StringInputSig stringInputSig, ISubsystemButton subsystemButton);

    internal class SubsystemButton : ISubsystemButton, IDisposable
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
                public const uint SelectSubsystem = 1;

                public const uint SubsystemSelected = 1;
            }
            internal static class Strings
            {

                public const uint SubsystemName = 1;
                public const uint SubsystemIcon = 2;
                public const uint SubsystemStatus = 3;
            }
        }

        #endregion

        #region Construction and Initialization

        internal SubsystemButton(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.SelectSubsystem, onSelectSubsystem);

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

        public event EventHandler<UIEventArgs> SelectSubsystem;
        private void onSelectSubsystem(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = SelectSubsystem;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void SubsystemSelected(SubsystemButtonBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.SubsystemSelected], this);
            }
        }


        public void SubsystemName(SubsystemButtonStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.SubsystemName], this);
            }
        }

        public void SubsystemIcon(SubsystemButtonStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.SubsystemIcon], this);
            }
        }

        public void SubsystemStatus(SubsystemButtonStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.SubsystemStatus], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "SubsystemButton", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            SelectSubsystem = null;
        }

        #endregion

    }
}
