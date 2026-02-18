# CH5 Contract Manual Modification Guide

This guide explains how to modify Crestron CH5 contracts without using the Contract Editor.

## File Structure

```
ACS_Contract.txt              - JSON contract definition (.cse2j file)
Contract/
├── Contract.g.cs             - Main contract class
├── ComponentMediator.g.cs    - Routes signals between UI and components
├── UIEventArgs.g.cs          - Event argument wrapper
├── Subsystem/                - Component folder
│   ├── SubsystemButton.g.cs  - Component class
│   └── SubsystemList.g.cs    - List metadata class
├── MusicControl/
├── Lights/
└── ... (other component folders)
```

## Understanding the Number System

### Smart Object IDs
Each component instance gets a unique **Smart Object ID**. These are the outer numbers in the JSON:

```json
"23": {                    // Smart Object ID 23 = MusicRoomControl[0]
    "1": "MusicRoomControl[0].musicZoneSelected",
    "4": "MusicRoomControl[0].musicZoneMuted"
}
```

### Join Numbers
Within each Smart Object, **Join Numbers** identify specific signals:
- Join 1 might be "Selected" boolean
- Join 2 might be "Name" string
- etc.

## How to Add a New Component

### Step 1: Update ACS_Contract.txt (JSON)

Add your component's signals. Example adding a new "CustomButton" with 5 instances starting at SmartObject ID 600:

```json
"signals": {
    "states": {
        "boolean": {
            // ... existing entries ...
            "600": {
                "1": "CustomButton[0].IsSelected"
            },
            "601": {
                "1": "CustomButton[1].IsSelected"
            },
            // ... continue for all instances
        },
        "string": {
            // ... existing entries ...
            "600": {
                "1": "CustomButton[0].Label"
            },
            // ...
        }
    },
    "events": {
        "boolean": {
            // ... existing entries ...
            "600": {
                "1": "CustomButton[0].Press"
            },
            // ...
        }
    }
}
```

**Signal Types:**
- `states.boolean` - Boolean feedback TO the UI (e.g., button selected state)
- `states.numeric` - Numeric feedback TO the UI (e.g., volume level)
- `states.string` - String feedback TO the UI (e.g., labels)
- `events.boolean` - Boolean input FROM the UI (e.g., button press)
- `events.numeric` - Numeric input FROM the UI (e.g., slider value)
- `events.string` - String input FROM the UI (e.g., text entry)

### Step 2: Create Component C# File

Create `Contract/CustomButtons/CustomButton.g.cs`:

```csharp
using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract.CustomButtons
{
    public interface ICustomButton
    {
        object UserObject { get; set; }
        
        // Events FROM UI
        event EventHandler<UIEventArgs> Press;
        
        // Feedback TO UI
        void IsSelected(CustomButtonBoolInputSigDelegate callback);
        void Label(CustomButtonStringInputSigDelegate callback);
    }

    public delegate void CustomButtonBoolInputSigDelegate(BoolInputSig boolInputSig, ICustomButton customButton);
    public delegate void CustomButtonStringInputSigDelegate(StringInputSig stringInputSig, ICustomButton customButton);

    internal class CustomButton : ICustomButton, IDisposable
    {
        private ComponentMediator ComponentMediator { get; set; }
        public object UserObject { get; set; }
        public uint ControlJoinId { get; private set; }
        private IList<BasicTriListWithSmartObject> _devices;
        public IList<BasicTriListWithSmartObject> Devices { get { return _devices; } }

        // Join numbers - MUST match the JSON!
        private static class Joins
        {
            internal static class Booleans
            {
                public const uint Press = 1;      // Event from UI
                public const uint IsSelected = 1; // State to UI
            }
            internal static class Strings
            {
                public const uint Label = 1;      // State to UI
            }
        }

        internal CustomButton(ComponentMediator componentMediator, uint controlJoinId)
        {
            ComponentMediator = componentMediator;
            ControlJoinId = controlJoinId;
            _devices = new List<BasicTriListWithSmartObject>();
            
            // Register event handlers for signals FROM the UI
            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.Press, onPress);
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

        // Event implementation
        public event EventHandler<UIEventArgs> Press;
        private void onPress(SmartObjectEventArgs eventArgs)
        {
            EventHandler<UIEventArgs> handler = Press;
            if (handler != null)
                handler(this, UIEventArgs.CreateEventArgs(eventArgs));
        }

        // State methods - send values TO the UI
        public void IsSelected(CustomButtonBoolInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.IsSelected], this);
            }
        }

        public void Label(CustomButtonStringInputSigDelegate callback)
        {
            for (int index = 0; index < Devices.Count; index++)
            {
                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.Label], this);
            }
        }

        public bool IsDisposed { get; set; }
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            Press = null;
        }
    }
}
```

### Step 3: Update Contract.g.cs

Add these sections to `Contract/Contract.g.cs`:

#### A. Add SmartObjectIdMappings (around line 110)
```csharp
private static readonly IDictionary<int, uint> CustomButtonSmartObjectIdMappings = new Dictionary<int, uint>{
    { 0, 600 }, { 1, 601 }, { 2, 602 }, { 3, 603 }, { 4, 604 }};
```

#### B. Add Properties (around line 26)
```csharp
public Ch5_Sample_Contract.CustomButtons.ICustomButton[] CustomButton { get { return InternalCustomButton.Cast<Ch5_Sample_Contract.CustomButtons.ICustomButton>().ToArray(); } }
private Ch5_Sample_Contract.CustomButtons.CustomButton[] InternalCustomButton { get; set; }
```

#### C. Initialize in Constructor (around line 190)
```csharp
InternalCustomButton = new Ch5_Sample_Contract.CustomButtons.CustomButton[CustomButtonSmartObjectIdMappings.Count];
for (int index = 0; index < CustomButtonSmartObjectIdMappings.Count; index++)
{
    InternalCustomButton[index] = new Ch5_Sample_Contract.CustomButtons.CustomButton(ComponentMediator, CustomButtonSmartObjectIdMappings[index]);
}
```

#### D. Add to AddDevice() (around line 290)
```csharp
for (int index = 0; index < 5; index++)
{
    InternalCustomButton[index].AddDevice(device);
}
```

#### E. Add to RemoveDevice() (around line 370)
```csharp
for (int index = 0; index < 5; index++)
{
    InternalCustomButton[index].RemoveDevice(device);
}
```

#### F. Add to Dispose() (around line 450)
```csharp
for (int index = 0; index < 5; index++)
{
    InternalCustomButton[index].Dispose();
}
```

#### G. Add to ClearDictionaries() (around line 275)
```csharp
CustomButtonSmartObjectIdMappings.Clear();
```

## How to Modify Existing Component (Add Signal)

1. **Update JSON**: Add the new join number to the appropriate section
2. **Update Component .g.cs**:
   - Add join constant to `Joins` class
   - Add event/method as needed
   - Wire up in Initialize() if it's an event

## How to Increase Instance Count

1. **Update JSON**: Add entries for new SmartObject IDs
2. **Update Contract.g.cs**:
   - Extend the SmartObjectIdMappings dictionary
   - Update the loop counts in AddDevice(), RemoveDevice(), Dispose()

## Finding Next Available SmartObject ID

Look at the highest number in ACS_Contract.txt. In your current contract, SmartObject IDs go up to 512, so start new components at 513 or higher.

## Using the ContractModifier Class

For programmatic modifications, use the provided `ContractModifier.cs` class:

```csharp
// Load contract
var contract = ContractModifier.LoadContractJson("ACS_Contract.txt");

// Add a component
ContractModifier.AddComponentToContract(
    contract,
    "CustomButton",
    startSmartObjectId: 600,
    count: 5,
    booleanStates: new Dictionary<uint, string> { { 1, "IsSelected" } },
    booleanEvents: new Dictionary<uint, string> { { 1, "Press" } },
    stringStates: new Dictionary<uint, string> { { 1, "Label" } }
);

// Save
ContractModifier.SaveContractJson(contract, "ACS_Contract.txt");

// Get next available ID
uint nextId = ContractModifier.GetNextSmartObjectId(contract);
```

## Common Patterns

### Simple Selection Button
- Boolean State Join 1: IsSelected (feedback)
- Boolean Event Join 1: Press (from UI)
- String State Join 1: Label

### Volume Control
- Boolean State Join 1: Selected
- Boolean State Join 4: Muted  
- Boolean Event Join 2: VolUp
- Boolean Event Join 3: VolDown
- Boolean Event Join 4: Mute
- Numeric State Join 1: Volume (0-65535)
- String State Join 1: Name

### Toggle Button
- Boolean State Join 1: IsOn (feedback)
- Boolean Event Join 1: Toggle (from UI)

## Tips

1. **Keep SmartObject IDs sequential** within a component for easier management
2. **Document your join assignments** - they must match between JSON and C#
3. **Use consistent join numbers** across similar components
4. **Test incrementally** - add one component, verify it works, then add more

