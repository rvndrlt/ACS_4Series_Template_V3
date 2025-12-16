using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.WholeHouseSubsystem
{
    public interface IWholeHouseSubsystem
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> SelectSubsystem;

        void SubsystemIsSelected(WholeHouseSubsystemBoolInputSigDelegate callback);
        void SubsystemName(WholeHouseSubsystemStringInputSigDelegate callback);
        void SubsystemIcon(WholeHouseSubsystemStringInputSigDelegate callback);

    }

    public delegate void WholeHouseSubsystemBoolInputSigDelegate(BoolInputSig boolInputSig, IWholeHouseSubsystem wholeHouseSubsystem);
    public delegate void WholeHouseSubsystemStringInputSigDelegate(StringInputSig stringInputSig, IWholeHouseSubsystem wholeHouseSubsystem);

    internal class WholeHouseSubsystem : IWholeHouseSubsystem, IDisposable
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

                public const uint SubsystemIsSelected = 1;
            }
            internal static class Strings
            {

                public const uint SubsystemName = 1;
                public const uint SubsystemIcon = 2;
            }
        }

        #endregion

        #region Construction and Initialization

        internal WholeHouseSubsystem(ComponentMediator componentMediator, uint controlJoinId)
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


        public void SubsystemIsSelected(WholeHouseSubsystemBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.SubsystemIsSelected], this);
            }
        }


        public void SubsystemName(WholeHouseSubsystemStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.SubsystemName], this);
            }
        }

        public void SubsystemIcon(WholeHouseSubsystemStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.SubsystemIcon], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "WholeHouseSubsystem", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
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
