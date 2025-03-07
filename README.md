
# KeyCodeInfo Plugin

**KeyCodeInfo** is a Rainmeter plugin written in C# that captures keyboard events and displays key information in various formats. It supports multiple output modes such as:
- **Decimal Mode (ShowCode=1):** Displays the key code as a decimal number.
- **Hexadecimal Mode (ShowCode=3):** Displays the key code in hexadecimal format (e.g., "0x4A").
- **Friendly Name Mode (ShowCode=0):** Displays a friendly key name (e.g., "Space", "CapsLock", "Shift").
- **Key Combination Mode (ShowCode=4):** Detects and displays the currently pressed keys as a combination (e.g., "Ctrl + Alt + A") in the order the keys were pressed.

The plugin also supports a **HideForce** parameter to control when the key information is cleared and includes bang commands to **Start** and **Stop** the plugin (which also start or stop a timer to update the Rainmeter skin automatically).

## Features

- **Multiple Output Modes:** Choose between decimal, hexadecimal, friendly name, or key combination output.
- **Key Combination Detection:** Displays keys in the order they are pressed (with modifiers retained if still physically pressed).
- **Automatic Measure Updates:** A built-in timer forces the Rainmeter measure and meters to update and redraw.
- **Bang Command Control:** Use Rainmeter bang commands (`!CommandMeasure`) to start and stop the plugin.
- **Reset on Reload:** Clears stored key values when the skin is refreshed.

## Prerequisites

- **Rainmeter:** [Download Rainmeter](https://www.rainmeter.net/)
- **C# Compiler / Visual Studio:** To compile the plugin.
- **DllExporter Tool:** To export the C# methods as unmanaged DLL exports (for example, [UnmanagedExports](https://www.nuget.org/packages/UnmanagedExports/)).

## Compilation Instructions

1. Open the solution in Visual Studio.
2. Ensure you have installed a DllExporter tool (such as UnmanagedExports) so that the plugin functions are properly exported.
3. Build the project targeting the desired architecture (x86 or x64).
4. The compiled `KeyCodeInfo.dll` will be created in your build output directory.

## Installation

1. **Copy the DLL:** Place the compiled `KeyCodeInfo.dll` into Rainmeter's Plugins folder. This is typically located at:
   ```
   Documents\Rainmeter\Plugins
   ```
2. **Refresh Rainmeter:** Right-click on the Rainmeter icon in the system tray and choose "Refresh All" to load the new plugin.

## Usage

### Adding the Measure to a Skin

Below is a sample Rainmeter skin snippet that demonstrates how to use the KeyCodeInfo plugin in key combination mode (ShowCode=4) with a background.

```ini
[Rainmeter]
Update=50
AccurateText=1

[Metadata]
Name=KeyCodeInfo - Key Combination Skin
Author=Your Name
Information=This skin demonstrates the KeyCodeInfo plugin in combination mode with a background.
Version=1.0

;-------------------------------------------------
; Background
;-------------------------------------------------
[MeterBackground]
Meter=Shape
Shape=Rectangle 0,0,400,200,10 | Fill Color 50,50,50,220 | StrokeWidth 0
W=400
H=200

;-------------------------------------------------
; Key Combination Measure
;-------------------------------------------------
[MeasureKeyCode]
Measure=Plugin
Plugin=KeyCodeInfo
; Use combination mode to display key combinations
ShowCode=4
; Set HideForce=0 to retain the combination until keys are released
HideForce=0

;-------------------------------------------------
; Display the key combination
;-------------------------------------------------
[MeterKeyCombination]
Meter=String
MeasureName=MeasureKeyCode
X=20
Y=20
FontSize=28
FontColor=255,255,255
AntiAlias=1
Text="Keys: %1"
DynamicVariables=1

;-------------------------------------------------
; Start Plugin Button
;-------------------------------------------------
[MeterStart]
Meter=String
X=20
Y=100
FontSize=16
FontColor=0,255,0
AntiAlias=1
Text="Start Plugin"
LeftMouseUpAction=!CommandMeasure MeasureKeyCode "Start"
DynamicVariables=1

;-------------------------------------------------
; Stop Plugin Button
;-------------------------------------------------
[MeterStop]
Meter=String
X=200
Y=100
FontSize=16
FontColor=255,0,0
AntiAlias=1
Text="Stop Plugin"
LeftMouseUpAction=!CommandMeasure MeasureKeyCode "Stop"
DynamicVariables=1
```

### Bang Commands

- **Start the Plugin:**
  ```
  !CommandMeasure MeasureKeyCode "Start"
  ```
  This command starts the keyboard hook, retrieves the measure name, and starts an update timer that forces the measure, all meters, and a redraw.

- **Stop the Plugin:**
  ```
  !CommandMeasure MeasureKeyCode "Stop"
  ```
  This command stops the keyboard hook and the update timer.

### Parameter Options

- **ShowCode Parameter:**  
  - `0` - Friendly key names.
  - `1` - Decimal key code.
  - `3` - Hexadecimal key code.
  - `4` - Key combination (displays keys in the order pressed).

- **HideForce Parameter:**  
  - `1` (default) - Clears key data immediately after reading (for modes 0, 1, and 3).
  - `0` - Retains the last key until a new key press occurs (useful for combination mode).

## Troubleshooting

- **Key Not Displaying:**  
  Ensure that the DLL is placed in the correct plugins folder and that Rainmeter has been refreshed.
- **Build Issues:**  
  Verify that you have installed the necessary DllExporter tool and that your project settings match your target platform (x86 or x64).

## Contributing

Contributions are welcome! Feel free to fork the repository, make improvements, and submit pull requests.

## License

This project is licensed under the MIT License – see the [LICENSE](LICENSE) file for details.

## Contact

For questions or support, please open an issue on GitHub.
