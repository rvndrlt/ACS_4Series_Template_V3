using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.HomePageMusicControl
{
    public interface IHomeMusicZone
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> SendPowerOff;
        event EventHandler<UIEventArgs> VolumeUp;
        event EventHandler<UIEventArgs> VolumeDown;
        event EventHandler<UIEventArgs> SendMute;
        event EventHandler<UIEventArgs> SetVolume;

        void isVisible(HomeMusicZoneBoolInputSigDelegate callback);
        void isMuted(HomeMusicZoneBoolInputSigDelegate callback);
        void Volume(HomeMusicZoneUShortInputSigDelegate callback);
        void ZoneName(HomeMusicZoneStringInputSigDelegate callback);
        void CurrentSource(HomeMusicZoneStringInputSigDelegate callback);

    }

    public delegate void HomeMusicZoneBoolInputSigDelegate(BoolInputSig boolInputSig, IHomeMusicZone homeMusicZone);
    public delegate void HomeMusicZoneUShortInputSigDelegate(UShortInputSig uShortInputSig, IHomeMusicZone homeMusicZone);
    public delegate void HomeMusicZoneStringInputSigDelegate(StringInputSig stringInputSig, IHomeMusicZone homeMusicZone);

    internal class HomeMusicZone : IHomeMusicZone, IDisposable
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
                public const uint SendPowerOff = 1;
                public const uint VolumeUp = 2;
                public const uint VolumeDown = 3;
                public const uint SendMute = 4;

                public const uint isVisible = 1;
                public const uint isMuted = 5;
            }
            internal static class Numerics
            {
                public const uint SetVolume = 1;

                public const uint Volume = 1;
            }
            internal static class Strings
            {
                public const uint ZoneName = 1;
                public const uint CurrentSource = 2;
            }
        }

        #endregion

        #region Construction and Initialization

        internal HomeMusicZone(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.SendPowerOff, onSendPowerOff);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.VolumeUp, onVolumeUp);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.VolumeDown, onVolumeDown);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.SendMute, onSendMute);
            ComponentMediator.ConfigureNumericEvent(controlJoinId, Joins.Numerics.SetVolume, onSetVolume);

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

        public event EventHandler<UIEventArgs> SendPowerOff;
        private void onSendPowerOff(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = SendPowerOff;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> VolumeUp;
        private void onVolumeUp(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = VolumeUp;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> VolumeDown;
        private void onVolumeDown(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = VolumeDown;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> SendMute;
        private void onSendMute(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = SendMute;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void isVisible(HomeMusicZoneBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.isVisible], this);
            }
        }

        public void isMuted(HomeMusicZoneBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.isMuted], this);
            }
        }

        public event EventHandler<UIEventArgs> SetVolume;
        private void onSetVolume(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = SetVolume;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void Volume(HomeMusicZoneUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.Volume], this);
            }
        }

        public void ZoneName(HomeMusicZoneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.ZoneName], this);
            }
        }

        public void CurrentSource(HomeMusicZoneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.CurrentSource], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "HomeMusicZone", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            SendPowerOff = null;
            VolumeUp = null;
            VolumeDown = null;
            SendMute = null;
            SetVolume = null;
        }

        #endregion

    }
}
