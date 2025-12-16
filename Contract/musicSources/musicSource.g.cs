using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.musicSources
{
    public interface ImusicSource
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> selectMusicSource;

        void musicSourceSelected(musicSourceBoolInputSigDelegate callback);
        void musicSourceName(musicSourceStringInputSigDelegate callback);
        void musicSourceIcon(musicSourceStringInputSigDelegate callback);

    }

    public delegate void musicSourceBoolInputSigDelegate(BoolInputSig boolInputSig, ImusicSource musicSource);
    public delegate void musicSourceStringInputSigDelegate(StringInputSig stringInputSig, ImusicSource musicSource);

    internal class musicSource : ImusicSource, IDisposable
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
                public const uint selectMusicSource = 1;

                public const uint musicSourceSelected = 1;
            }
            internal static class Strings
            {

                public const uint musicSourceName = 1;
                public const uint musicSourceIcon = 2;
            }
        }

        #endregion

        #region Construction and Initialization

        internal musicSource(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.selectMusicSource, onselectMusicSource);

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

        public event EventHandler<UIEventArgs> selectMusicSource;
        private void onselectMusicSource(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = selectMusicSource;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void musicSourceSelected(musicSourceBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.musicSourceSelected], this);
            }
        }


        public void musicSourceName(musicSourceStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.musicSourceName], this);
            }
        }

        public void musicSourceIcon(musicSourceStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.musicSourceIcon], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "musicSource", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            selectMusicSource = null;
        }

        #endregion

    }
}
