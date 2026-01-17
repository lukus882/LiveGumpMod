// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Network.ModernUO
{
    /// <summary>
    /// Constants for LiveGump 0xBF Extended Commands.
    /// Sub-commands 0x0100-0x0107 are used for LiveGump operations.
    /// </summary>
    internal static class LiveGumpConstants
    {
        /// <summary>
        /// 0xBF Extended Commands sub-commands for LiveGump.
        /// </summary>
        public static class SubCommand
        {
            public const ushort SetProperty = 0x0100;
            public const ushort AddElement = 0x0101;
            public const ushort RemoveElement = 0x0102;
            public const ushort Animation = 0x0105;
            public const ushort Refresh = 0x0106;
            public const ushort Close = 0x0107;
        }

        /// <summary>
        /// Element types for dynamic element creation.
        /// </summary>
        public static class ElementType
        {
            public const byte Label = 0x01;
            public const byte HtmlText = 0x02;
            public const byte Image = 0x03;
            public const byte Button = 0x04;
            public const byte TextEntry = 0x05;
        }

        /// <summary>
        /// Property IDs for element property updates.
        /// </summary>
        public static class PropertyId
        {
            public const byte Text = 0x01;
            public const byte Hue = 0x02;
            public const byte Visible = 0x03;
            public const byte X = 0x04;
            public const byte Y = 0x05;
            public const byte Width = 0x06;
            public const byte Height = 0x07;
            public const byte Graphic = 0x08;
        }
    }
}
