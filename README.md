# PokeMMO Quest Tracker - Remastered Edition 🎮

A fully remastered, modern, and native overlay for PokeMMO. 
This project overhauls the original quest tracker by introducing seamless UI integration, native universal controller support, and a complete aesthetic revamp.

![PokeMMO Tracker Preview](https://via.placeholder.com/800x400.png?text=Add+a+screenshot+here+later!)

## 🌟 Key Features

### 1. Universal Controller Support (Native SDL2)
Say goodbye to third-party scripts, Python bridges, or DS4Windows. 
The tracker now uses **SDL2** natively within the C# application to support virtually any controller out of the box (DualShock 4, DualSense, Nintendo Switch Pro Controller, Xbox, and generic gamepads).
* **Toggle Mode:** Press **`L3 + R3`** simultaneously to switch focus between the game and the tracker seamlessly (with audio feedback).
* **D-Pad Navigation:** Move smoothly through your quest list and bottom buttons using your controller's D-Pad.
* **Quick Actions:** Use the **`South Button (A/Cross)`** to check/uncheck quests, and **`Shoulder Buttons (L1/R1)`** to switch between regions.

### 2. "Big Picture" Borderless Overlay
The clunky Windows title bar has been removed. The tracker now operates as a true **borderless, semi-transparent overlay** that floats elegantly over your game.
* **Invisible Drag Handle:** Click and hold the subtle "DRAG HERE" zone at the top to move the tracker anywhere on your screen.
* **Resizeable:** Grab the bottom-right corner to scale the UI to your preference.
* **Always on Top:** The UI stays pinned over PokeMMO without obstructing your view thanks to its dark, translucent aesthetic.

### 3. Modern Aesthetics
* **Embedded Poppins Font:** The UI now exclusively uses the *Poppins* typeface, embedded directly into the application. No system installation required.
* **Rounded UI:** Checkboxes and task borders have been redesigned with a card-like appearance, increased padding, soft corner radii, and drop shadows for optimal readability.

---

## 🚀 How to Run & Build

Because this is a `.NET 8.0` WPF application, you can easily build it yourself from the source code.

### Prerequisites
* [Download and install the .NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### Build Instructions
1. Clone this repository to your local machine.
2. Open a terminal (PowerShell/CMD) in the root directory of the project.
3. Run the following command:
   ```bash
   dotnet build -c Release
   ```
4. Navigate to `bin\Release\net8.0-windows\` and run **`PokeMMOTracker.exe`**.

*Note: The required `SQLite` and `SDL2` native dependencies are automatically copied to your output folder during the build process.*

---

## 🎮 Controller Shortcuts (Tracker Mode)

| Action | Controller Input |
| :--- | :--- |
| **Enter/Exit Tracker Mode** | `L3 + R3` (Click both sticks) |
| **Navigate Up/Down** | `D-Pad Up` / `D-Pad Down` |
| **Navigate Buttons (Left/Right)** | `D-Pad Left` / `D-Pad Right` (When at the bottom of the list) |
| **Check / Uncheck Quest** | `South Button` (A on Xbox / Cross on PS) |
| **Next Region** | `R1 / RB` |
| **Previous Region** | `L1 / LB` |

---

## 🗺️ Roadmap (Coming Soon)

We are constantly looking to improve the PokeMMO Quest Tracker. Here is what's planned for future updates:

* [ ] **Dynamic Theme Engine:** Allow users to switch between light mode, dark mode, or custom region-based color palettes directly from the UI.
* [ ] **Auto-Snapping Improvements:** Re-introduce a safe, multi-monitor friendly auto-snapping feature to automatically pin the tracker to the active PokeMMO window.
* [ ] **Live Progress Sync:** Investigate safe, non-intrusive (Read-Only/OCR based) methods to auto-check quests without risking account bans.
* [ ] **Custom Quest Editor:** Allow users to add their own custom routes or shiny-hunting checklists via the UI.

---

*Disclaimer: This tool operates completely externally. It does not inject into, read from, or modify the PokeMMO process memory, ensuring it strictly adheres to PokeMMO's Terms of Service regarding third-party software.*
