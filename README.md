# LiveGump System for ModernUO + ClassicUO

Real-time gump updates without full gump refresh. Update individual labels, images, and elements on the fly.

## What It Does

- Update gump text/hue without closing and reopening the gump
- Perfect for game stats, timers, progress bars, live displays
- Uses standard UO packet 0xBF (Extended Commands)

---

## Installation

### Server Side (ModernUO)


1. Copy `LiveGumpCore.cs` to `Projects/UOContent/CUSTOM/LiveGumps/`
2. That's it - the server is ready

### Client Side (ClassicUO)

1. Copy these files to `src/ClassicUO.Client/Network/ModernUO/`:
   - `LiveGumpConstants.cs`
   - `ModernUOPacketHandlers.cs`

2. Edit `PacketHandlers.cs` - find the `ExtendedCommand` method and add these cases:

```csharp
// LiveGump sub-commands (0x100-0x107)
case 0x0100: // SetProperty
case 0x0101: // AddElement
case 0x0102: // RemoveElement
case 0x0105: // Animation
case 0x0106: // Refresh
case 0x0107: // Close
    ModernUOPacketHandlers.HandleLiveGumpExtendedCommand(world, cmd, ref p);
    break;
```

3. In the same file, find the `CreateGump` method. After `UIManager.Add(gump);` add:

```csharp
ModernUOPacketHandlers.TrackGump(gump);
```

4. Rebuild ClassicUO: `dotnet build`

---

## Quick Start



### Create a LiveGump

```csharp
using Server.LiveGumps;

public class MyStatsGump : LiveGump
{
    public override uint GumpTypeId => 0x12345678; // Unique ID
    
    private uint _scoreLabel;
    private uint _healthLabel;
    
    public MyStatsGump(Mobile viewer) : base(viewer, 100, 100) { }
    
    protected override void BuildGump()
    {
        AddBackground(0, 0, 200, 80, 9270);
        AddLabel(10, 10, 0x480, "Score:");
        _scoreLabel = AddLiveLabel(70, 10, 0x35, "0");
        AddLabel(10, 35, 0x480, "Health:");
        _healthLabel = AddLiveLabel(70, 35, 0x35, "100");
    }
    
    protected override void OnAutoRefresh()
    {
        // Called automatically when auto-refresh is enabled
        UpdateText(_scoreLabel, GetPlayerScore().ToString());
        UpdateTextWithHue(_healthLabel, GetPlayerHealth().ToString(), GetHealthHue());
    }
}
```

### Open the Gump

```csharp
var gump = new MyStatsGump(player);
gump.Open();
gump.StartAutoRefresh(500); // Update every 500ms
```

### Manual Updates

```csharp
gump.UpdateText(_scoreLabel, "1000");
gump.UpdateHue(_scoreLabel, 0x35);
gump.Refresh(); // Send all pending updates
```

---

## API Reference

### Adding Elements

```csharp
// Returns element ID for later updates
uint id = AddLiveLabel(x, y, hue, "text", "name");
uint id = AddLiveImage(x, y, gumpId, hue, "name");
uint id = AddLiveHtml(x, y, width, height, "html", background, scrollbar, "name");
uint id = AddLiveButton(x, y, normalId, pressedId, buttonId, type, param, "name");
```

### Updating Elements

```csharp
UpdateText(elementId, "new text");          // Update by ID
UpdateText("elementName", "new text");      // Update by name
UpdateHue(elementId, 0x35);                 // Change color
UpdateVisible(elementId, true/false);       // Show/hide
UpdateTextWithHue(elementId, "text", 0x35); // Update text and hue together
```

### Refresh & Auto-refresh

```csharp
Refresh();              // Send all pending updates to client
StartAutoRefresh(1000); // Auto-refresh every 1000ms
StopAutoRefresh();      // Stop auto-refresh

// Override to add update logic
protected override void OnAutoRefresh()
{
    // Called automatically when auto-refresh is enabled
}
```

### Lifecycle

```csharp
Open();   // Opens the gump
Close();  // Closes the gump
```

---

## Protocol Details

Uses packet `0xBF` (Extended Commands) with sub-commands `0x0100-0x0107`:

| Sub-Cmd | Name | Description |
|---------|------|-------------|
| 0x0100 | SetProperty | Update element property (text, hue, visibility, etc.) |
| 0x0101 | AddElement | Dynamically add element |
| 0x0102 | RemoveElement | Remove element |
| 0x0105 | Animation | Trigger animation effect |
| 0x0106 | Refresh | Request full refresh |
| 0x0107 | Close | Close the gump |

### Property IDs (for SetProperty)

| ID | Property |
|----|----------|
| 0x01 | Text |
| 0x02 | Hue |
| 0x03 | Visible |
| 0x04 | X |
| 0x05 | Y |
| 0x06 | Width |
| 0x07 | Height |
| 0x08 | Graphic |

---

## Troubleshooting

**Gump not updating?**
- Make sure you call `Refresh()` after updates (or use `StartAutoRefresh`)
- Verify the element ID is correct
- Check that the client has the LiveGump modifications

**Wrong element updating?**
- Element IDs match the order elements are added to the gump
- Use `AddLiveLabel` (not `AddLabel`) for elements you want to update

---

## Files Included

### Server (ModernUO)
- `LiveGumpCore.cs` - Core framework

### Client (ClassicUO)
- `LiveGumpConstants.cs` - Protocol constants
- `ModernUOPacketHandlers.cs` - Packet handlers
