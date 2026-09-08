//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.Menus.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Music menu navigation for HTML panels — the share-source list, the home-music control
    /// dialog, the add-to-group room picker and the change-group-source picker.
    ///
    /// WHY THIS IS NOT JUST "MOVE THE BOOLEANS INTO JSON"
    ///
    /// The video dropdowns (lift/format/sleep) became pure panel state because the program has
    /// never cared whether they were open. These menus are different: the program builds their
    /// room list and SUBSCRIBES to per-room volume/mute/source events while they are up, then
    /// releases those subscriptions when they close (see TouchpanelUI.MusicSharing.cs). It
    /// genuinely needs to know.
    ///
    /// So the direction of authority changes rather than disappearing:
    ///
    ///   BEFORE  the program set b 998/999/21/1500/1503 and treated its own write as the truth.
    ///           A panel that closed a menu locally desynced the program — the reason
    ///           homeMusicControlBindingS2's click-outside close had to be disabled: hiding the
    ///           DOM left join 21 latched true and the menu would not reopen.
    ///
    ///   AFTER   the program COMMANDS an open or close on serial 1526, and the panel REPORTS
    ///           what is actually on screen on serial 1527. The program's open-set is built
    ///           from those reports, so a local close (X, Done, tap outside) is a first-class
    ///           event that unwinds subscriptions correctly instead of a desync.
    ///
    /// One menu per message, because they stack: add-to-group opens ON TOP of the scenario-2
    /// control dialog, which is why a single "which menu is open" slot could not express it.
    /// `seq` makes a repeated identical command still fire — the panel dedupes on it.
    /// </summary>
    public partial class TouchpanelUI
    {
        // C# -> HTML: open/close a named menu.
        private const ushort MenuCommandJoin = 1526;
        // HTML -> C#: "this menu is now open/closed". Raw serial from the panel, routed in
        // UserInterfaceObject_SigChange exactly like the quick-actions (1531), cameras (1542)
        // and intercom (1561) command channels.
        public const ushort MenuStateJoin = 1527;

        // Canonical menu keys. Shared with the HTML modules; keep in sync.
        public const string MenuShareSource       = "shareSource";
        public const string MenuHomeMusicControl  = "homeMusicControl";
        public const string MenuAddToGroup        = "addToGroup";
        public const string MenuChangeGroupSource = "changeGroupSource";
        /// <summary>Wildcard for "close everything" (Home press, power off, room change).</summary>
        public const string MenuAll = "*";

        private ushort _menuSeq;

        /// <summary>
        /// Menus the PANEL says are on screen. Built only from 1527 reports, never from what we
        /// commanded — a command can be superseded by the user before it lands, and the whole
        /// point of the reverse channel is that the panel is the authority on its own display.
        /// </summary>
        private readonly HashSet<string> _openMenus = new HashSet<string>();

        /// <summary>True if the panel has reported this menu open.</summary>
        public bool IsMenuOpen(string menu)
        {
            return _openMenus.Contains(menu);
        }

        /// <summary>
        /// True if any music selection menu is on screen. Replaces the old
        /// "b21 || b998 || b999 || b1500" test in musicPageFlips, which read back joins this
        /// program had written and so could not see a menu the panel had closed by itself.
        /// </summary>
        public bool AnyMusicMenuOpen()
        {
            return _openMenus.Count > 0;
        }

        /// <summary>
        /// Commands the panel to open or close a menu. HTML panels only — dumb panels keep the
        /// boolean joins, which every caller still writes alongside this.
        /// </summary>
        /// <param name="menu">One of the Menu* constants, or MenuAll with open=false.</param>
        /// <param name="variant">
        /// Layout selector where a menu has more than one — currently only share-source
        /// ("floors" / "nofloors"). Empty for menus with a single layout.
        /// </param>
        public void SendMenuCommand(string menu, bool open, string variant)
        {
            if (!this.HTML_UI) return;

            _menuSeq++;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"seq\":").Append(_menuSeq);
            sb.Append(",\"menu\":\"").Append(menu).Append("\"");
            sb.Append(",\"open\":").Append(open ? "true" : "false");
            sb.Append(",\"variant\":\"").Append(variant ?? string.Empty).Append("\"");
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[MenuCommandJoin].StringValue = json;
            if (_parent.logging) CrestronConsole.PrintLine("TP-{0} menuCommand -> {1}", this.Number, json);

            // A commanded CLOSE is also recorded immediately rather than waiting for the
            // report. The panel does send one, but close paths (Home, power off) are often
            // followed synchronously by work that asks AnyMusicMenuOpen(), and that must not
            // see a menu we have just torn down. An open is NOT recorded here — it is not on
            // screen until the panel says so.
            if (!open)
            {
                if (menu == MenuAll) { _openMenus.Clear(); }
                else { _openMenus.Remove(menu); }
            }
        }

        /// <summary>Convenience overload for menus with a single layout.</summary>
        public void SendMenuCommand(string menu, bool open)
        {
            SendMenuCommand(menu, open, string.Empty);
        }

        /// <summary>
        /// Drops the open-set WITHOUT commanding the panel to close anything.
        ///
        /// For the one case where the menus are already gone and there is nobody to tell: an
        /// HTML reload. The panel comes back with a blank slate and no memory of what it had
        /// open, so it never reports those menus closed — leaving stale entries that would
        /// suppress the media player indefinitely. Called from the page-ready pull (1521).
        /// </summary>
        public void ForgetOpenMenus()
        {
            if (_openMenus.Count == 0) return;
            CrestronConsole.PrintLine("TP-{0} forgetting {1} stale open menu(s) after HTML reload",
                this.Number, _openMenus.Count);
            _openMenus.Clear();
        }

        /// <summary>
        /// Closes every music menu on this panel. The analogue of the scattered
        /// "998 = false; 999 = false; 21 = false" blocks in the navigation handlers.
        /// </summary>
        public void CloseAllMusicMenus()
        {
            SendMenuCommand(MenuAll, false);
        }

        /// <summary>
        /// Handles a menu-state report from the panel on serial 1527:
        ///   {"menu":"shareSource","open":false}
        ///
        /// This is where a LOCAL close (the X button, Done, a tap outside the dialog) becomes
        /// something the program acts on. Each menu's unwind is the same work its programmatic
        /// close already does — dropping room subscriptions, clearing staged state — so this
        /// routes to those existing methods rather than duplicating them.
        /// </summary>
        public void HandleMenuState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            string menu = ExtractJsonString(json, "menu");
            if (string.IsNullOrEmpty(menu)) return;
            bool open = json.Contains("\"open\":true");

            CrestronConsole.PrintLine("TP-{0} menuState <- menu={1} open={2}", this.Number, menu, open);

            bool was = _openMenus.Contains(menu);
            if (open) { _openMenus.Add(menu); }
            else { _openMenus.Remove(menu); }

            // Only act on a real transition to closed. The panel re-reports state on reconnect,
            // and unwinding subscriptions twice would be harmless but noisy.
            if (open || !was) return;

            try
            {
                if (menu == MenuShareSource)
                {
                    // Same unwind HandleSharingButton does when it toggles the menu off.
                    this.SrcSharingButtonFB = false;
                    this.UserInterface.BooleanInput[1002].BoolValue = false;
                    UnsubscribeFromMusicSharingChanges();
                }
                else if (menu == MenuAddToGroup)
                {
                    _parent.CloseAddToGroupMenu(this.Number);
                }
                else if (menu == MenuChangeGroupSource)
                {
                    _parent.CloseChangeGroupSourceMenu(this.Number);
                }
                else if (menu == MenuHomeMusicControl)
                {
                    // No subscriptions of its own to release — the zone list it shows is
                    // rebuilt on open — but the program tracks it so the media player does not
                    // pop up over it (see musicPageFlips), which _openMenus now covers.
                    ClearMusicSourcePage();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TP-{0} HandleMenuState({1}) error: {2}", this.Number, menu, ex.Message);
            }
        }

        /// <summary>
        /// Pulls a string value out of a flat JSON object without a parser. These payloads are
        /// two fields written by our own HTML, so a full deserializer would cost more than it
        /// is worth here — the same reasoning the descriptor builders use in the other
        /// direction (StringBuilder rather than a serializer).
        /// </summary>
        private static string ExtractJsonString(string json, string key)
        {
            string token = "\"" + key + "\"";
            int k = json.IndexOf(token, StringComparison.Ordinal);
            if (k < 0) return string.Empty;

            int colon = json.IndexOf(':', k + token.Length);
            if (colon < 0) return string.Empty;

            int open = json.IndexOf('"', colon + 1);
            if (open < 0) return string.Empty;

            int close = json.IndexOf('"', open + 1);
            if (close < 0) return string.Empty;

            return json.Substring(open + 1, close - open - 1);
        }
    }
}
