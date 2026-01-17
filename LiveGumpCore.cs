/*
 * ModernUO Live Gump Framework v3.0
 * 
 * Real-time gump updates using packet 0xBF (Extended Commands).
 * Works with modified ClassicUO client.
 * 
 * Usage:
 * - Inherit from LiveGump and override BuildGump()
 * - Use AddLiveLabel/AddLiveImage to create updateable elements
 * - Call UpdateText/UpdateHue to modify elements
 * - Call Refresh() or use StartAutoRefresh() to send updates
 */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Server.Gumps;
using Server.Network;

namespace Server.LiveGumps;

#region Protocol Constants

/// <summary>
/// Protocol constants for LiveGump packets.
/// Uses 0xBF (Extended Commands) with custom sub-commands for server-to-client LiveGump updates.
/// Sub-commands 0x100-0x1FF are reserved for LiveGump to avoid conflicts with standard UO commands.
/// </summary>
public static class LiveGumpProtocol
{
    /// <summary>Packet ID - 0xBF (Extended Commands packet).</summary>
    public const byte PacketId = 0xBF;

    // Sub-commands (0x100+ reserved for LiveGump to avoid conflicts with standard 0xBF sub-commands)
    public const ushort SubCmd_SetProperty = 0x0100;
    public const ushort SubCmd_AddElement = 0x0101;
    public const ushort SubCmd_RemoveElement = 0x0102;
    public const ushort SubCmd_ProgressBar = 0x0103;
    public const ushort SubCmd_Timer = 0x0104;
    public const ushort SubCmd_Animation = 0x0105;
    public const ushort SubCmd_Refresh = 0x0106;
    public const ushort SubCmd_Close = 0x0107;

    // Property IDs
    public const byte Prop_Text = 0x01;
    public const byte Prop_Hue = 0x02;
    public const byte Prop_Visible = 0x03;
    public const byte Prop_X = 0x04;
    public const byte Prop_Y = 0x05;
    public const byte Prop_Width = 0x06;
    public const byte Prop_Height = 0x07;
    public const byte Prop_Graphic = 0x08;
}

#endregion

#region Enums

public enum LiveAnimation : byte
{
    None = 0,
    FadeIn = 1,
    FadeOut = 2,
    Pulse = 3,
    Flash = 4,
    Shake = 5,
    Scale = 6,
    ColorPulse = 7,
    Bounce = 8
}

#endregion

#region Packet Sending

/// <summary>
/// Static methods for sending LiveGump packets using 0xBF Extended Commands.
/// </summary>
public static class LiveGumpPackets
{
    /// <summary>Enable debug logging for LiveGump packets.</summary>
    public static bool DebugEnabled { get; set; } = true;

    /// <summary>Send text property update.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SendText(NetState ns, uint gumpSerial, uint elementIndex, string text)
    {
        if (ns == null) return;

        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        // 0xBF packet: ID(1) + Len(2) + SubCmd(2) + GumpSerial(4) + ElementId(4) + PropId(1) + TextLen(2) + Text
        int len = 3 + 2 + 4 + 4 + 1 + 2 + bytes.Length;

        if (DebugEnabled)
        {
            Console.WriteLine($"[LiveGump] SendText: GumpSerial=0x{gumpSerial:X8}, Element={elementIndex}, Text=\"{text}\", PacketLen={len}");
        }

        var buf = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            int p = 0;
            buf[p++] = LiveGumpProtocol.PacketId;
            buf[p++] = (byte)(len >> 8);
            buf[p++] = (byte)len;
            WriteU16(buf, ref p, LiveGumpProtocol.SubCmd_SetProperty);
            WriteU32(buf, ref p, gumpSerial);
            WriteU32(buf, ref p, elementIndex);
            buf[p++] = LiveGumpProtocol.Prop_Text;
            WriteU16(buf, ref p, (ushort)bytes.Length);
            bytes.CopyTo(buf.AsSpan(p));
            ns.Send(buf.AsSpan(0, len));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    /// <summary>Send hue property update.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SendHue(NetState ns, uint gumpSerial, uint elementIndex, ushort hue)
    {
        if (ns == null) return;

        // 0xBF packet: ID(1) + Len(2) + SubCmd(2) + GumpSerial(4) + ElementId(4) + PropId(1) + Hue(2)
        const int len = 16;
        Span<byte> buf = stackalloc byte[len];
        int p = 0;
        buf[p++] = LiveGumpProtocol.PacketId;
        buf[p++] = 0;
        buf[p++] = len;
        WriteU16(buf, ref p, LiveGumpProtocol.SubCmd_SetProperty);
        WriteU32(buf, ref p, gumpSerial);
        WriteU32(buf, ref p, elementIndex);
        buf[p++] = LiveGumpProtocol.Prop_Hue;
        WriteU16(buf, ref p, hue);
        ns.Send(buf);
    }

    /// <summary>Send visibility property update.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SendVisible(NetState ns, uint gumpSerial, uint elementIndex, bool visible)
    {
        if (ns == null) return;

        // 0xBF packet: ID(1) + Len(2) + SubCmd(2) + GumpSerial(4) + ElementId(4) + PropId(1) + Visible(1)
        const int len = 15;
        Span<byte> buf = stackalloc byte[len];
        int p = 0;
        buf[p++] = LiveGumpProtocol.PacketId;
        buf[p++] = 0;
        buf[p++] = len;
        WriteU16(buf, ref p, LiveGumpProtocol.SubCmd_SetProperty);
        WriteU32(buf, ref p, gumpSerial);
        WriteU32(buf, ref p, elementIndex);
        buf[p++] = LiveGumpProtocol.Prop_Visible;
        buf[p++] = (byte)(visible ? 1 : 0);
        ns.Send(buf);
    }

    /// <summary>Send animation trigger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SendAnimation(NetState ns, uint gumpSerial, uint elementIndex, byte animType, ushort duration)
    {
        if (ns == null) return;

        // 0xBF packet: ID(1) + Len(2) + SubCmd(2) + GumpSerial(4) + ElementId(4) + AnimType(1) + Duration(2)
        const int len = 16;
        Span<byte> buf = stackalloc byte[len];
        int p = 0;
        buf[p++] = LiveGumpProtocol.PacketId;
        buf[p++] = 0;
        buf[p++] = len;
        WriteU16(buf, ref p, LiveGumpProtocol.SubCmd_Animation);
        WriteU32(buf, ref p, gumpSerial);
        WriteU32(buf, ref p, elementIndex);
        buf[p++] = animType;
        WriteU16(buf, ref p, duration);
        ns.Send(buf);
    }

    /// <summary>Send text and hue together.</summary>
    public static void SendTextWithHue(NetState ns, uint gumpSerial, uint elementIndex, string text, ushort hue)
    {
        SendText(ns, gumpSerial, elementIndex, text);
        SendHue(ns, gumpSerial, elementIndex, hue);
    }

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU32(Span<byte> buf, ref int p, uint v)
    {
        buf[p++] = (byte)(v >> 24);
        buf[p++] = (byte)(v >> 16);
        buf[p++] = (byte)(v >> 8);
        buf[p++] = (byte)v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU32(byte[] buf, ref int p, uint v)
    {
        buf[p++] = (byte)(v >> 24);
        buf[p++] = (byte)(v >> 16);
        buf[p++] = (byte)(v >> 8);
        buf[p++] = (byte)v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU16(Span<byte> buf, ref int p, ushort v)
    {
        buf[p++] = (byte)(v >> 8);
        buf[p++] = (byte)v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU16(byte[] buf, ref int p, ushort v)
    {
        buf[p++] = (byte)(v >> 8);
        buf[p++] = (byte)v;
    }

    #endregion

    #region Legacy Compatibility

    // These methods maintain backward compatibility with existing code
    public static void SendSetPropertyText(NetState ns, uint gumpId, uint elemId, string text) =>
        SendText(ns, gumpId, elemId, text);

    public static void SendSetPropertyUInt16(NetState ns, uint gumpId, uint elemId, byte propId, ushort value)
    {
        if (propId == LiveGumpProtocol.Prop_Hue)
            SendHue(ns, gumpId, elemId, value);
    }

    public static void SendSetPropertyByte(NetState ns, uint gumpId, uint elemId, byte propId, byte value)
    {
        if (propId == LiveGumpProtocol.Prop_Visible)
            SendVisible(ns, gumpId, elemId, value != 0);
    }

    public static void SendSetPropertyInt16(NetState ns, uint gumpId, uint elemId, byte propId, short value) =>
        SendSetPropertyUInt16(ns, gumpId, elemId, propId, (ushort)value);

    public static void SendProgressBarUpdate(NetState ns, uint gumpId, uint elemId, int current, int max)
    {
        // Simplified - send as text percentage
        int pct = max > 0 ? (current * 100 / max) : 0;
        SendText(ns, gumpId, elemId, $"{pct}%");
    }

    public static void SendTimerUpdate(NetState ns, uint gumpId, uint elemId, int seconds, bool isPaused)
    {
        string text = isPaused ? "PAUSED" : $"{seconds}s";
        SendText(ns, gumpId, elemId, text);
    }

    public static void SendAnimationTrigger(NetState ns, uint gumpId, uint elemId, byte animType, ushort duration) =>
        SendAnimation(ns, gumpId, elemId, animType, duration);

    #endregion
}

#endregion

#region LiveElement (for backward compatibility)

/// <summary>
/// Represents an updateable element in a gump.
/// </summary>
public sealed class LiveElement
{
    public uint Id { get; }
    public string Name { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Text { get; set; }
    public ushort Hue { get; set; }
    public ushort Graphic { get; set; }
    public byte Alpha { get; set; } = 255;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public int Value { get; set; }
    public int MaxValue { get; set; }

    public LiveElement(uint id, string name = null)
    {
        Id = id;
        Name = name ?? $"elem_{id}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetInitialValues(int x, int y, string text, ushort hue)
    {
        X = x;
        Y = y;
        Text = text;
        Hue = hue;
    }
}

#endregion

#region LiveGump Base (backward compatibility)

/// <summary>
/// Base class for gumps with real-time updates.
/// Maintained for backward compatibility - prefer event-driven approach.
/// </summary>
public abstract class LiveGump : Gump
{
    private readonly Dictionary<uint, LiveElement> _elements = new();
    private readonly List<Action<NetState>> _updates = new();
    private Timer _timer;

    public abstract uint GumpTypeId { get; }
    public Mobile Viewer { get; }
    public bool AutoRefreshEnabled { get; private set; }
    public int AutoRefreshInterval { get; private set; } = 1000;

    protected LiveGump(Mobile viewer, int x = 100, int y = 100) : base(x, y)
    {
        Viewer = viewer;
        TypeID = (int)GumpTypeId;
        Closable = true;
        Disposable = true;
        Draggable = true;
        Resizable = false;
    }

    protected abstract void BuildGump();
    protected virtual void OnAutoRefresh() { }

    public void Initialize() => BuildGump();

    #region Element Management

    /// <summary>
    /// Gets the current number of entries in the gump.
    /// This is used to determine the next element index.
    /// </summary>
    private uint GetNextElementIndex() => (uint)Entries.Count;

    protected uint RegisterElement(string name = null)
    {
        // Use the current gump entry count as the element ID
        // This ensures the ID matches the actual index in the gump
        uint id = GetNextElementIndex();
        _elements[id] = new LiveElement(id, name);
        return id;
    }

    protected uint AddLiveLabel(int x, int y, int hue, string text, string name = null)
    {
        // Get the ID BEFORE adding the label (it will be the index of the label we're about to add)
        uint id = GetNextElementIndex();
        _elements[id] = new LiveElement(id, name);
        _elements[id].SetInitialValues(x, y, text, (ushort)hue);
        AddLabel(x, y, hue, text);
        return id;
    }

    protected uint AddLiveHtml(int x, int y, int width, int height, string html, bool background, bool scrollbar, string name = null)
    {
        uint id = GetNextElementIndex();
        _elements[id] = new LiveElement(id, name);
        _elements[id].SetInitialValues(x, y, html, 0);
        _elements[id].Width = width;
        _elements[id].Height = height;
        AddHtml(x, y, width, height, html, background, scrollbar);
        return id;
    }

    protected uint AddLiveImage(int x, int y, int gumpId, int hue = 0, string name = null)
    {
        uint id = GetNextElementIndex();
        _elements[id] = new LiveElement(id, name);
        _elements[id].SetInitialValues(x, y, null, (ushort)hue);
        _elements[id].Graphic = (ushort)gumpId;
        if (hue > 0) AddImage(x, y, gumpId, hue);
        else AddImage(x, y, gumpId);
        return id;
    }

    protected uint AddLiveButton(int x, int y, int normalId, int pressedId, int buttonId, GumpButtonType type, int param, string name = null)
    {
        uint id = GetNextElementIndex();
        _elements[id] = new LiveElement(id, name);
        _elements[id].SetInitialValues(x, y, null, 0);
        _elements[id].Graphic = (ushort)normalId;
        AddButton(x, y, normalId, pressedId, buttonId, type, param);
        return id;
    }

    public LiveElement GetElement(uint id) => _elements.TryGetValue(id, out var e) ? e : null;

    public LiveElement GetElement(string name)
    {
        foreach (var e in _elements.Values)
            if (e.Name == name) return e;
        return null;
    }

    #endregion

    #region Update Methods

    public void UpdateText(uint id, string text)
    {
        if (_elements.TryGetValue(id, out var e))
        {
            e.Text = text;
            // Use TypeID for client routing - client's ServerSerial = TypeID from gump packet
            _updates.Add(ns => LiveGumpPackets.SendText(ns, (uint)TypeID, id, text));
        }
    }

    public void UpdateText(string name, string text)
    {
        var e = GetElement(name);
        if (e != null) UpdateText(e.Id, text);
    }

    public void UpdateHue(uint id, ushort hue)
    {
        if (_elements.TryGetValue(id, out var e))
        {
            e.Hue = hue;
            // Use TypeID for client routing - client's ServerSerial = TypeID from gump packet
            _updates.Add(ns => LiveGumpPackets.SendHue(ns, (uint)TypeID, id, hue));
        }
    }

    public void UpdateVisible(uint id, bool visible)
    {
        if (_elements.TryGetValue(id, out var e))
        {
            e.Visible = visible;
            // Use TypeID for client routing - client's ServerSerial = TypeID from gump packet
            _updates.Add(ns => LiveGumpPackets.SendVisible(ns, (uint)TypeID, id, visible));
        }
    }

    public void UpdateTextWithHue(uint id, string text, ushort hue)
    {
        UpdateText(id, text);
        UpdateHue(id, hue);
    }

    public void TriggerAnimation(uint id, LiveAnimation anim, ushort durationMs)
    {
        // Use TypeID for client routing - client's ServerSerial = TypeID from gump packet
        _updates.Add(ns => LiveGumpPackets.SendAnimation(ns, (uint)TypeID, id, (byte)anim, durationMs));
    }

    public void UpdateProgress(uint id, int current, int max)
    {
        if (_elements.TryGetValue(id, out var e))
        {
            e.Value = current;
            e.MaxValue = max;
            // Use TypeID for client routing - client's ServerSerial = TypeID from gump packet
            _updates.Add(ns => LiveGumpPackets.SendProgressBarUpdate(ns, (uint)TypeID, id, current, max));
        }
    }

    public void UpdateTimer(uint id, int remainingSeconds, bool isPaused = false)
    {
        if (_elements.TryGetValue(id, out var e))
        {
            e.Value = remainingSeconds;
            // Use Serial (gump serial) for client routing - client matches by ServerSerial
            _updates.Add(ns => LiveGumpPackets.SendTimerUpdate(ns, (uint)Serial, id, remainingSeconds, isPaused));
        }
    }

    #endregion

    #region Refresh Control

    public void Refresh()
    {
        var ns = Viewer?.NetState;
        if (ns == null || _updates.Count == 0)
        {
            _updates.Clear();
            return;
        }

        foreach (var update in _updates)
            update(ns);

        _updates.Clear();
    }

    public void StartAutoRefresh(int intervalMs = 1000)
    {
        StopAutoRefresh();
        if (Viewer?.NetState == null)
            return;

        AutoRefreshEnabled = true;
        AutoRefreshInterval = intervalMs;
        _timer = Timer.DelayCall(
            TimeSpan.FromMilliseconds(intervalMs),
            TimeSpan.FromMilliseconds(intervalMs),
            OnTimerTick
        );
    }

    private void OnTimerTick()
    {
        if (Viewer?.NetState == null || !AutoRefreshEnabled)
        {
            StopAutoRefresh();
            return;
        }

        try
        {
            OnAutoRefresh();
            Refresh();
        }
        catch
        {
            StopAutoRefresh();
        }
    }

    public void StopAutoRefresh()
    {
        AutoRefreshEnabled = false;
        _timer?.Stop();
        _timer = null;
    }

    internal void InvokeAutoRefresh()
    {
        OnAutoRefresh();
        Refresh();
    }

    #endregion

    #region Lifecycle

    public void Open()
    {
        if (Viewer?.NetState == null) return;
        Viewer.NetState.SendCloseGump(TypeID, 0);
        Initialize();
        Viewer.SendGump(this);
    }

    public new void Close()
    {
        StopAutoRefresh();
        Viewer?.NetState?.SendCloseGump(TypeID, 0);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0)
            StopAutoRefresh();
        OnGumpResponse(sender.Mobile, info.ButtonID, info.Switches, info);
    }

    protected virtual void OnGumpResponse(Mobile from, int buttonId, ReadOnlySpan<int> switches, in RelayInfo info) { }

    #endregion

    #region Public Wrappers

    public uint AddLiveLabelPublic(int x, int y, int hue, string text, string name = null) =>
        AddLiveLabel(x, y, hue, text, name);

    public uint AddLiveImagePublic(int x, int y, int gumpId, int hue = 0, string name = null) =>
        AddLiveImage(x, y, gumpId, hue, name);

    public uint AddLiveButtonPublic(int x, int y, int normalId, int pressedId, int buttonId, string name = null) =>
        AddLiveButton(x, y, normalId, pressedId, buttonId, GumpButtonType.Reply, 0, name);

    public uint AddLiveHtmlPublic(int x, int y, int width, int height, string html, bool background, bool scrollbar, string name = null) =>
        AddLiveHtml(x, y, width, height, html, background, scrollbar, name);

    #endregion
}

#endregion

#region Enums for backward compatibility

public enum ProgressBarStyle : byte
{
    Default = 0,
    Rounded = 1,
    Segmented = 2,
    Gradient = 3,
    Minimal = 4
}

public enum TimerDisplayMode : byte
{
    SecondsOnly = 0,
    MinutesSeconds = 1,
    HoursMinutesSeconds = 2,
    Compact = 3
}

#endregion
