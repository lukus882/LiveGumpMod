// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO;
using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClassicUO.Network.ModernUO
{
    /// <summary>
    /// Handles LiveGump updates via 0xBF Extended Commands packet.
    /// Sub-commands 0x0100-0x0107 are used for LiveGump operations.
    /// </summary>
    internal static class ModernUOPacketHandlers
    {
        private static bool DebugEnabled => ProfileManager.CurrentProfile?.DebugLiveGumps ?? false;

        // Track gumps by their server-assigned serial for quick lookup
        private static readonly Dictionary<uint, Gump> _trackedGumps = new();

        // Constants for validation
        private const int MaxTextLength = 8192;

        #region Gump Tracking

        /// <summary>
        /// Registers a gump for live updates. Called when any gump is opened.
        /// </summary>
        public static void TrackGump(Gump gump)
        {
            if (gump == null || gump.ServerSerial == 0)
                return;

            _trackedGumps[gump.ServerSerial] = gump;
            Log.Info($"ModernUO: TrackGump - ServerSerial: 0x{gump.ServerSerial:X8}, Type: {gump.GetType().Name}");
        }

        /// <summary>
        /// Unregisters a gump from live updates. Called when any gump is closed.
        /// </summary>
        public static void UntrackGump(Gump gump)
        {
            if (gump == null)
                return;

            _trackedGumps.Remove(gump.ServerSerial);
            _trackedGumps.Remove(gump.LocalSerial);
        }

        /// <summary>
        /// Clears all tracked gumps. Called on disconnect.
        /// </summary>
        public static void ClearTrackedGumps()
        {
            _trackedGumps.Clear();
        }

        #endregion

        #region 0xBF Extended Command Handler

        /// <summary>
        /// Handles LiveGump commands from 0xBF Extended Commands packet.
        /// Called from PacketHandlers.ExtendedCommand for sub-commands 0x0100-0x0107.
        /// </summary>
        public static void HandleLiveGumpExtendedCommand(World world, ushort subCommand, ref StackDataReader p)
        {
            Log.Info($"ModernUO: 0xBF SubCmd 0x{subCommand:X4}, Remaining: {p.Remaining}");

            switch (subCommand)
            {
                case 0x0100: // SetProperty
                    HandleSetProperty(world, ref p);
                    break;
                case 0x0101: // AddElement
                    HandleAddElement(world, ref p);
                    break;
                case 0x0102: // RemoveElement
                    HandleRemoveElement(world, ref p);
                    break;
                case 0x0105: // Animation
                    HandleAnimation(world, ref p);
                    break;
                case 0x0106: // Refresh
                    HandleRefresh(world, ref p);
                    break;
                case 0x0107: // Close
                    HandleClose(world, ref p);
                    break;
                default:
                    Log.Warn($"ModernUO: Unknown 0xBF sub-command: 0x{subCommand:X4}");
                    break;
            }
        }

        #endregion

        #region Packet Handlers

        private static void HandleSetProperty(World world, ref StackDataReader p)
        {
            if (p.Remaining < 9)
            {
                Log.Warn($"ModernUO: SetProperty packet too short - need 9, have {p.Remaining}");
                return;
            }

            uint gumpSerial = p.ReadUInt32BE();
            uint elementId = p.ReadUInt32BE();
            byte propertyId = p.ReadUInt8();

            Log.Info($"ModernUO: SetProperty - Gump: 0x{gumpSerial:X8}, Element: {elementId}, Property: 0x{propertyId:X2}");

            var gump = FindGump(gumpSerial);
            if (gump == null)
            {
                LogOpenGumps(gumpSerial);
                return;
            }

            var element = FindElement(gump, elementId);
            if (element == null)
            {
                Log.Warn($"ModernUO: Element {elementId} not found in gump. Children: {gump.Children.Count}");
                return;
            }

            Log.Info($"ModernUO: Found element {element.GetType().Name}, updating property");
            UpdateProperty(element, propertyId, ref p);

            gump.WantUpdateSize = true;
            gump.RequestUpdateContents();
        }

        private static void HandleAddElement(World world, ref StackDataReader p)
        {
            if (p.Remaining < 11)
            {
                Log.Warn("ModernUO: AddElement packet too short");
                return;
            }

            uint gumpSerial = p.ReadUInt32BE();
            uint elementId = p.ReadUInt32BE();
            byte elementType = p.ReadUInt8();
            short x = p.ReadInt16BE();
            short y = p.ReadInt16BE();

            Log.Info($"ModernUO: AddElement - Gump: 0x{gumpSerial:X8}, Element: {elementId}, Type: 0x{elementType:X2}");

            var gump = FindGump(gumpSerial);
            if (gump == null)
                return;

            Control newElement = CreateElement(elementType, elementId, x, y, ref p);
            if (newElement != null)
            {
                gump.Add(newElement);
                gump.RequestUpdateContents();
            }
        }

        private static void HandleRemoveElement(World world, ref StackDataReader p)
        {
            if (p.Remaining < 8)
            {
                Log.Warn("ModernUO: RemoveElement packet too short");
                return;
            }

            uint gumpSerial = p.ReadUInt32BE();
            uint elementId = p.ReadUInt32BE();

            Log.Info($"ModernUO: RemoveElement - Gump: 0x{gumpSerial:X8}, Element: {elementId}");

            var gump = FindGump(gumpSerial);
            if (gump == null)
                return;

            var element = FindElement(gump, elementId);
            if (element != null)
            {
                element.Dispose();
                gump.RequestUpdateContents();
            }
        }

        private static void HandleAnimation(World world, ref StackDataReader p)
        {
            if (p.Remaining < 11)
            {
                Log.Warn("ModernUO: Animation packet too short");
                return;
            }

            uint gumpSerial = p.ReadUInt32BE();
            uint elementId = p.ReadUInt32BE();
            byte animationType = p.ReadUInt8();
            ushort duration = p.ReadUInt16BE();

            Log.Info($"ModernUO: Animation - Gump: 0x{gumpSerial:X8}, Element: {elementId}, Type: {animationType}");

            var gump = FindGump(gumpSerial);
            if (gump == null)
                return;

            var element = FindElement(gump, elementId);
            if (element is ILiveAnimatable animatable)
            {
                animatable.TriggerAnimation(animationType, duration);
            }
        }

        private static void HandleRefresh(World world, ref StackDataReader p)
        {
            if (p.Remaining < 4)
            {
                Log.Warn("ModernUO: Refresh packet too short");
                return;
            }

            uint gumpSerial = p.ReadUInt32BE();
            Log.Info($"ModernUO: Refresh - Gump: 0x{gumpSerial:X8}");

            var gump = FindGump(gumpSerial);
            gump?.RequestUpdateContents();
        }

        private static void HandleClose(World world, ref StackDataReader p)
        {
            if (p.Remaining < 4)
            {
                Log.Warn("ModernUO: Close packet too short");
                return;
            }

            uint gumpSerial = p.ReadUInt32BE();
            Log.Info($"ModernUO: Close - Gump: 0x{gumpSerial:X8}");

            var gump = FindGump(gumpSerial);
            if (gump != null)
            {
                UntrackGump(gump);
                gump.Dispose();
            }
        }

        #endregion

        #region Gump/Element Finding

        private static Gump FindGump(uint serial)
        {
            // Check tracked gumps first
            if (_trackedGumps.TryGetValue(serial, out var tracked) && !tracked.IsDisposed)
            {
                return tracked;
            }

            // Search all open gumps
            foreach (var gump in UIManager.Gumps)
            {
                if (gump.IsDisposed)
                    continue;

                if (gump.ServerSerial == serial || gump.LocalSerial == serial)
                {
                    return gump;
                }
            }

            return null;
        }

        private static Control FindElement(Gump gump, uint elementId)
        {
            // Try by LocalSerial first
            var found = FindElementBySerial(gump, elementId);
            if (found != null)
                return found;

            // Fall back to index-based lookup
            if (elementId < (uint)gump.Children.Count)
            {
                return gump.Children[(int)elementId];
            }

            return null;
        }

        private static Control FindElementBySerial(Control parent, uint elementId)
        {
            if (parent.LocalSerial == elementId)
                return parent;

            foreach (var child in parent.Children)
            {
                var found = FindElementBySerial(child, elementId);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void LogOpenGumps(uint searchedSerial)
        {
            Log.Warn($"ModernUO: Gump not found - Serial: 0x{searchedSerial:X8}. Open gumps:");
            foreach (var g in UIManager.Gumps)
            {
                if (!g.IsDisposed)
                {
                    Log.Info($"  - {g.GetType().Name}: ServerSerial=0x{g.ServerSerial:X8}, LocalSerial=0x{g.LocalSerial:X8}");
                }
            }
            Log.Info($"ModernUO: Tracked gumps: {_trackedGumps.Count}");
            foreach (var kvp in _trackedGumps)
            {
                Log.Info($"  - 0x{kvp.Key:X8} -> {kvp.Value?.GetType().Name ?? "null"}");
            }
        }

        #endregion

        #region Property Updates

        private static void UpdateProperty(Control element, byte propertyId, ref StackDataReader p)
        {
            switch (propertyId)
            {
                case 0x01: // Text
                    if (p.Remaining < 2) return;
                    ushort textLen = p.ReadUInt16BE();
                    if (textLen > MaxTextLength) textLen = MaxTextLength;
                    if (p.Remaining < textLen) return;

                    string text = Encoding.UTF8.GetString(p.ReadArray(textLen));

                    if (element is Label label)
                        label.Text = text;
                    else if (element is HtmlControl html)
                        html.Text = text;
                    else if (element is StbTextBox textBox)
                        textBox.SetText(text);

                    Log.Info($"ModernUO: Updated text to '{text}'");
                    break;

                case 0x02: // Hue
                    if (p.Remaining < 2) return;
                    ushort hue = p.ReadUInt16BE();

                    if (element is Label lbl)
                        lbl.Hue = hue;
                    else if (element is GumpPic pic)
                        pic.Hue = hue;
                    else if (element is StaticPic sPic)
                        sPic.Hue = hue;
                    break;

                case 0x03: // Visible
                    if (p.Remaining < 1) return;
                    element.IsVisible = p.ReadUInt8() != 0;
                    break;

                case 0x04: // X
                    if (p.Remaining < 2) return;
                    element.X = p.ReadInt16BE();
                    break;

                case 0x05: // Y
                    if (p.Remaining < 2) return;
                    element.Y = p.ReadInt16BE();
                    break;

                case 0x06: // Width
                    if (p.Remaining < 2) return;
                    element.Width = p.ReadInt16BE();
                    break;

                case 0x07: // Height
                    if (p.Remaining < 2) return;
                    element.Height = p.ReadInt16BE();
                    break;

                case 0x08: // Graphic
                    if (p.Remaining < 2) return;
                    ushort graphic = p.ReadUInt16BE();
                    if (element is GumpPic gp)
                        gp.Graphic = graphic;
                    else if (element is StaticPic sp)
                        sp.Graphic = graphic;
                    break;

                default:
                    Log.Warn($"ModernUO: Unknown property ID 0x{propertyId:X2}");
                    break;
            }
        }

        #endregion

        #region Element Creation

        private static Control CreateElement(byte elementType, uint elementId, short x, short y, ref StackDataReader p)
        {
            switch (elementType)
            {
                case 0x01: // Label
                    if (p.Remaining < 2) return null;
                    ushort textLen = p.ReadUInt16BE();
                    if (p.Remaining < textLen) return null;
                    string text = Encoding.UTF8.GetString(p.ReadArray(textLen));
                    ushort hue = p.Remaining >= 2 ? p.ReadUInt16BE() : (ushort)0xFFFF;
                    byte font = p.Remaining >= 1 ? p.ReadUInt8() : (byte)0xFF;

                    return new Label(text, true, hue, font: font)
                    {
                        X = x,
                        Y = y,
                        LocalSerial = elementId
                    };

                case 0x03: // Image
                    if (p.Remaining < 2) return null;
                    ushort graphic = p.ReadUInt16BE();
                    ushort imageHue = p.Remaining >= 2 ? p.ReadUInt16BE() : (ushort)0;

                    return new GumpPic(x, y, graphic, imageHue)
                    {
                        LocalSerial = elementId
                    };

                default:
                    Log.Warn($"ModernUO: Unknown element type 0x{elementType:X2}");
                    return null;
            }
        }

        #endregion
    }

    #region Interfaces for Live Elements

    /// <summary>
    /// Interface for elements that support animations.
    /// </summary>
    public interface ILiveAnimatable
    {
        void TriggerAnimation(byte animationType, ushort durationMs);
    }

    #endregion
}
