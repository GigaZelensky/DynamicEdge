# DynamicEdge 🛡️

**DynamicEdge** is a lightweight, high-performance utility for Windows that adds a satisfying, physics-based resistance barrier between multi-monitor setups. 

Unlike the default Windows "sticky corners" (which are often buggy, laggy, or inconsistent), DynamicEdge uses raw mouse input data to simulate a "membrane" between your screens. You can rest your cursor against the edge without slipping over, but a firm push or a fast flick will break through instantly.

### 🚀 Key Features

*   **Physics-Based Barrier:** The edge isn't just a wall; it has "health." Moving slowly drains the barrier's health, allowing you to push through intentionally, while a fast flick breaks it instantly.
*   **Fully Configurable:** Tune the barrier strength, regeneration rate, break threshold, and polling speeds via a dedicated **Settings** menu.
*   **Vertical & Offset Support:** Works perfectly with **vertically stacked** monitors and screens with significant **offsets/gaps**. No more cursor getting stuck in the "dead space" between displays.
*   **Zero Lag:** Uses `RawInput` for high-frequency mouse velocity tracking, bypassing standard Windows cursor ballistics for instant reaction times.
*   **Ultra-Efficient:** Runs with **EcoQoS** (Efficiency Mode) and idle priority. Uses adaptive polling (drops to 200ms checks when away from edges, ramps up to 10ms when active).
*   **Conflict Free:** Automatically suppresses the buggy native Windows 10/11 "Ease cursor movement" and "Sticky Edges" settings to prevent double-resistance.

### 🎮 How It Works

DynamicEdge treats the border between your monitors like a physical membrane:

1.  **The Wall:** When your cursor hits the edge of a screen, the software uses the Windows API `ClipCursor` to physically confine the mouse to that monitor.
2.  **The Force:** It listens to `WM_INPUT` (Raw Input) to calculate how hard you are pushing against that invisible wall.
3.  **The Breakthrough:**
    *   **Pressure:** Pushing consistently damages the membrane. Once "health" hits zero, the wall drops, and your cursor slides through.
    *   **Velocity:** A fast flick (high velocity) deals massive damage, breaking the wall instantly for a seamless transition.
4.  **Regeneration:** If you stop pushing, the membrane regenerates health rapidly, ready for the next interaction.

### 📥 Installation & Usage

1.  **Download:** Grab the latest `DynamicEdge.exe` from the releases page.
2.  **Run:** Double-click the application. It will minimize to the System Tray (near the clock).
3.  **That's it!** The barrier is now active.

**System Tray Options:**
*   **Toggle Edge:** Temporarily disable/enable the barrier.
*   **Settings...:** Open the configuration window to adjust physics and sensitivity.
*   **Start with Windows:** Sets the application to launch automatically on boot.

### 🛠️ Technical Requirements

*   **OS:** Windows 10 or Windows 11.
*   **Runtime:** .NET Framework 4.7.2 or higher (Standard on modern Windows).
*   **Monitors:** 2 or more displays.

### 🐛 Troubleshooting

**My cursor slides through instantly on vertical monitors!**
DynamicEdge uses `System.Windows.Forms.Screen.FromPoint` to detect which monitor you are on. Ensure your monitors are arranged correctly in **Windows Display Settings** without massive gaps between them.

**It feels like there is no resistance.**
You might be flicking too fast! Try moving the mouse slowly against the edge. You can also increase **Max Health** or the **Break Threshold** in the Settings menu.

**Something isn't working correctly.**
Check the logs folder at `%APPDATA%\DynamicEdge\logs` for error details. You can also use the **Reset** button in Settings to restore default values.

### 📄 License

MIT License. Feel free to modify, distribute, or use this code in your own projects.

---

**Created with C#, Win32 APIs, and a love for precise cursor control.**