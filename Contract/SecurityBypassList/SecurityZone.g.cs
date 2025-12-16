using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.SecurityBypassList
{
    public interface ISecurityZone
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> ZoneBypassTog;

        void ZoneBypassed(SecurityZoneBoolInputSigDelegate callback);
        void Zone_Visible(SecurityZoneBoolInputSigDelegate callback);
        void ZoneName(SecurityZoneStringInputSigDelegate callback);

    }

    public delegate void SecurityZoneBoolInputSigDelegate(BoolInputSig boolInputSig, ISecurityZone securityZone);
    public delegate void SecurityZoneStringInputSigDelegate(StringInputSig stringInputSig, ISecurityZone securityZone);

    internal class SecurityZone : ISecurityZone, IDisposable
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
                public const uint ZoneBypassTog = 1;

                public const uint ZoneBypassed = 1;
                public const uint Zone_Visible = 2;
            }
            internal static class Strings
            {

                public const uint ZoneName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal SecurityZone(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.ZoneBypassTog, onZoneBypassTog);

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

        public event EventHandler<UIEventArgs> ZoneBypassTog;
        private void onZoneBypassTog(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = ZoneBypassTog;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void ZoneBypassed(SecurityZoneBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.ZoneBypassed], this);
            }
        }

        public void Zone_Visible(SecurityZoneBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.Zone_Visible], this);
            }
        }


        public void ZoneName(SecurityZoneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.ZoneName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "SecurityZone", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            ZoneBypassTog = null;
        }

        #endregion

    }
}
