using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.MediaPlayer
{
    public interface IMediaPlayerObject
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> CRPC_TX;
        event EventHandler<UIEventArgs> MESSAGE_TX;

        void REFRESH(MediaPlayerObjectBoolInputSigDelegate callback);
        void OFFLINE(MediaPlayerObjectBoolInputSigDelegate callback);
        void USE_MESSAGE(MediaPlayerObjectBoolInputSigDelegate callback);
        void CRPC_RX(MediaPlayerObjectStringInputSigDelegate callback);
        void MESSAGE_RX(MediaPlayerObjectStringInputSigDelegate callback);
        void PLAYER_NAME(MediaPlayerObjectStringInputSigDelegate callback);

    }

    public delegate void MediaPlayerObjectBoolInputSigDelegate(BoolInputSig boolInputSig, IMediaPlayerObject mediaPlayerObject);
    public delegate void MediaPlayerObjectStringInputSigDelegate(StringInputSig stringInputSig, IMediaPlayerObject mediaPlayerObject);

    internal class MediaPlayerObject : IMediaPlayerObject, IDisposable
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
                public const uint REFRESH = 1;
                public const uint OFFLINE = 2;
                public const uint USE_MESSAGE = 3;
            }
            internal static class Strings
            {
                public const uint CRPC_TX = 1;
                public const uint MESSAGE_TX = 2;

                public const uint CRPC_RX = 1;
                public const uint MESSAGE_RX = 2;
                public const uint PLAYER_NAME = 3;
            }
        }

        #endregion

        #region Construction and Initialization

        internal MediaPlayerObject(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureStringEvent(controlJoinId, Joins.Strings.CRPC_TX, onCRPC_TX);
            ComponentMediator.ConfigureStringEvent(controlJoinId, Joins.Strings.MESSAGE_TX, onMESSAGE_TX);

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

        public void REFRESH(MediaPlayerObjectBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.REFRESH], this);
            }
        }

        public void OFFLINE(MediaPlayerObjectBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.OFFLINE], this);
            }
        }

        public void USE_MESSAGE(MediaPlayerObjectBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.USE_MESSAGE], this);
            }
        }

        public event EventHandler<UIEventArgs> CRPC_TX;
        private void onCRPC_TX(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = CRPC_TX;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        public event EventHandler<UIEventArgs> MESSAGE_TX;
        private void onMESSAGE_TX(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = MESSAGE_TX;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void CRPC_RX(MediaPlayerObjectStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.CRPC_RX], this);
            }
        }

        public void MESSAGE_RX(MediaPlayerObjectStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.MESSAGE_RX], this);
            }
        }

        public void PLAYER_NAME(MediaPlayerObjectStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.PLAYER_NAME], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "MediaPlayerObject", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            CRPC_TX = null;
            MESSAGE_TX = null;
        }

        #endregion

    }
}
