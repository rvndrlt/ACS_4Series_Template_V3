using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Video
{
    public class VideoSigChange
    {
        private ControlSystem _parent;
        public VideoSigChange(ControlSystem parent)
        {
            _parent = parent;
        }
        public void Video1SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 100)
                {
                    if (args.Sig.BoolValue == true)
                    {
                        ushort TPNumber = (ushort)(args.Sig.Number);
                        _parent.manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum++;//more vsrcs button pressed so increment the group number
                        //group = (ushort)(tpConfigs[TPNumber].currentVSrcGroupNum + 1);
                        _parent.SetVSRCGroup(TPNumber, _parent.manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum);
                    }
                }
                else if (args.Sig.Number <= 200 && args.Sig.BoolValue == true)
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    _parent.videoSystemControl.TurnOffAllDisplays(TPNumber);
                }
            }
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a video source
                {
                    CrestronConsole.PrintLine("TP-{0} select vsrc{1}", args.Sig.Number, args.Sig.UShortValue);
                    _parent.videoSystemControl.SelectVideoSourceFromTP((ushort)args.Sig.Number, args.Sig.UShortValue);
                }
                else if (args.Sig.Number > 500 && ControlSystem.initComplete)
                {
                    CrestronConsole.PrintLine("dm output changed------------ {0} {1} {2}", args.Sig.Number, args.Sig.UShortValue, ControlSystem.initComplete);
                    _parent.DmOutputChanged((ushort)args.Sig.Number, args.Sig.UShortValue);

                }
            }
        }
        public void Video2SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//not currently used
                {

                }
                else if (args.Sig.Number <= 200)//a video display is being selected
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    ushort ButtonNumber = (ushort)(args.Sig.UShortValue);
                    _parent.videoSystemControl.SelectDisplay(TPNumber, ButtonNumber);
                }
                else if (args.Sig.Number <= 300)//not currently used
                {

                }
                else if (args.Sig.Number <= 400)//not currently used
                {

                }
                else if (args.Sig.Number <= 500)
                {
                    ushort displayNumber = (ushort)(args.Sig.Number - 400);
                    _parent.videoSystemControl.SelectDisplayVideoSource(displayNumber, args.Sig.UShortValue);//display number / button number / this is used if the program needs to turn off a tv or send a source outside of a remote.
                }
            }
        }
        /// <summary>
        /// /we are currently only sending status to this EISC. we're not reading anything from it.
        /// </summary>
        /// <param name="currentDevice"></param>
        /// <param name="args"></param>
        public void Video3SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {

        }
    }
}
