using ACS_4Series_Template_V3.UI;
using Crestron.SimplSharp;
using Independentsoft.Json.Parser;
using System;

namespace ACS_4Series_Template_V3
{
    public class TSR_310
    {
        private readonly ControlSystem _parent;
        private readonly TouchpanelUI _tp;

        public TSR_310(ControlSystem parent, TouchpanelUI tp)
        {
            _parent = parent;
            _tp = tp;
        }

        public void HomeButtonPress()
        {
            ushort TPNumber = _tp.Number;
            CrestronConsole.PrintLine("TSR-310 TP-{0} HomeButtonPress", TPNumber);

            _tp.CurrentPageNumber = 2;


            _tp.musicPageFlips(0);
            _tp.subsystemPageFlips(0);

        }

    }
}
