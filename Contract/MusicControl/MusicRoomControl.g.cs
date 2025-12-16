using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.MusicControl
{
    public interface IMusicRoomControl
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> selectMusicZone;
        event EventHandler<UIEventArgs> musicVolUp;
        event EventHandler<UIEventArgs> musicVolDown;
        event EventHandler<UIEventArgs> muteMusicZone;
        event EventHandler<UIEventArgs> turnMusicZoneOff;

        void musicZoneSelected(MusicRoomControlBoolInputSigDelegate callback);
        void musicZoneMuted(MusicRoomControlBoolInputSigDelegate callback);
        void musicZoneOff(MusicRoomControlBoolInputSigDelegate callback);
        void musicVolEnable(MusicRoomControlBoolInputSigDelegate callback);
        void musicVolume(MusicRoomControlUShortInputSigDelegate callback);
        void musicZoneName(MusicRoomControlStringInputSigDelegate callback);
        void musicZoneSource(MusicRoomControlStringInputSigDelegate callback);

    }

    public delegate void MusicRoomControlBoolInputSigDelegate(BoolInputSig boolInputSig, IMusicRoomControl musicRoomControl);
    public delegate void MusicRoomControlUShortInputSigDelegate(UShortInputSig uShortInputSig, IMusicRoomControl musicRoomControl);
    public delegate void MusicRoomControlStringInputSigDelegate(StringInputSig stringInputSig, IMusicRoomControl musicRoomControl);

    internal class MusicRoomControl : IMusicRoomControl, IDisposable
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
                public const uint selectMusicZone = 1;
                public const uint musicVolUp = 2;
                public const uint musicVolDown = 3;
                public const uint muteMusicZone = 4;
                public const uint turnMusicZoneOff = 5;

                public const uint musicZoneSelected = 1;
                public const uint musicZoneMuted = 4;
                public const uint musicZoneOff = 5;
                public const uint musicVolEnable = 6;
            }
            internal static class Numerics
            {
                public const uint musicVolume = 1;
            }
            internal static class Strings
            {

                public const uint musicZoneName = 1;
                public const uint musicZoneSource = 2;
            }
        }

        #endregion

        #region Construction and Initialization

        internal MusicRoomControl(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.selectMusicZone, onselectMusicZone);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.musicVolUp, onmusicVolUp);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.musicVolDown, onmusicVolDown);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.muteMusicZone, onmuteMusicZone);
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.turnMusicZoneOff, onturnMusicZoneOff);

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

        public event EventHandler<UIEventArgs> selectMusicZone;
        private void onselectMusicZone(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = selectMusicZone;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> musicVolUp;
        private void onmusicVolUp(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = musicVolUp;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> musicVolDown;
        private void onmusicVolDown(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = musicVolDown;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> muteMusicZone;
        private void onmuteMusicZone(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = muteMusicZone;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> turnMusicZoneOff;
        private void onturnMusicZoneOff(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = turnMusicZoneOff;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void musicZoneSelected(MusicRoomControlBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.musicZoneSelected], this);
            }
        }

        public void musicZoneMuted(MusicRoomControlBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.musicZoneMuted], this);
            }
        }

        public void musicZoneOff(MusicRoomControlBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.musicZoneOff], this);
            }
        }

        public void musicVolEnable(MusicRoomControlBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.musicVolEnable], this);
            }
        }

        public void musicVolume(MusicRoomControlUShortInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.musicVolume], this);
            }
        }


        public void musicZoneName(MusicRoomControlStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.musicZoneName], this);
            }
        }

        public void musicZoneSource(MusicRoomControlStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.musicZoneSource], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "MusicRoomControl", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            selectMusicZone = null;
            musicVolUp = null;
            musicVolDown = null;
            muteMusicZone = null;
            turnMusicZoneOff = null;
        }

        #endregion

    }
}
