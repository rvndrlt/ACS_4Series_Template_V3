using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.LightsScenario2
{
    public interface ILightingScene
    {
        object UserObject { get; set; }

        event EventHandler<UIEventArgs> selectScene;

        void sceneIsActive(LightingSceneBoolInputSigDelegate callback);
        void sceneName(LightingSceneStringInputSigDelegate callback);

    }

    public delegate void LightingSceneBoolInputSigDelegate(BoolInputSig boolInputSig, ILightingScene lightingScene);
    public delegate void LightingSceneStringInputSigDelegate(StringInputSig stringInputSig, ILightingScene lightingScene);

    internal class LightingScene : ILightingScene, IDisposable
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
                public const uint selectScene = 1;

                public const uint sceneIsActive = 1;
            }
            internal static class Strings
            {

                public const uint sceneName = 1;
            }
        }

        #endregion

        #region Construction and Initialization

        internal LightingScene(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            Initialize(controlJoinId);
        }

        private void Initialize(uint controlJoinId)
        {
            ControlJoinId = controlJoinId; 
 
            _devices = new List<BasicTriListWithSmartObject>(); 
 
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.selectScene, onselectScene);

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

        public event EventHandler<UIEventArgs> selectScene;
        private void onselectScene(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = selectScene;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }


        public void sceneIsActive(LightingSceneBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.sceneIsActive], this);
            }
        }


        public void sceneName(LightingSceneStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.sceneName], this);
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
            return string.Format("Contract: {0} Component: {1} HashCode: {2} {3}", "LightingScene", GetType().Name, GetHashCode(), UserObject != null ? "UserObject: " + UserObject : null);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            selectScene = null;
        }

        #endregion

    }
}
