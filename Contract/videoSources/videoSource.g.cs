using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.videoSources
{
    public interface IvideoSource
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> vidSelectSource;

        void vidSourceIsSelected(videoSourceBoolInputSigDelegate callback);
        void vidSourceName(videoSourceStringInputSigDelegate callback);
        void vidSourceIcon(videoSourceStringInputSigDelegate callback);

    }

    public delegate void videoSourceBoolInputSigDelegate(BoolInputSig boolInputSig, IvideoSource videoSource);
    public delegate void videoSourceStringInputSigDelegate(StringInputSig stringInputSig, IvideoSource videoSource);

    internal class videoSource : IvideoSource, IDisposable
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
                public const uint vidSelectSource = 1;

                public const uint vidSourceIsSelected = 1;
            }
            internal static class Strings
            {

                public const uint vidSourceName = 1;
                public const uint vidSourceIcon = 2;
            }
        }

        #endregion

        #region Construction and Initialization

        internal videoSource(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.vidSelectSource, onvidSelectSource);

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

        public event EventHandler<UIEventArgs> vidSelectSource;
        private void onvidSelectSource(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = vidSelectSource;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void vidSourceIsSelected(videoSourceBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.vidSourceIsSelected], this);
            }
        }


        public void vidSourceName(videoSourceStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.vidSourceName], this);
            }
        }

        public void vidSourceIcon(videoSourceStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.vidSourceIcon], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "videoSource", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            vidSelectSource = null;
        }

        #endregion

    }
}
