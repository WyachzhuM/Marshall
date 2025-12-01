# Marshall: Python Script Management Application

<img width="50" height="50" alt="Icon" src="https://github.com/user-attachments/assets/efe29f1c-7319-49d0-8afd-36bd89cf239e" />

[**Marshall**](https://github.com/LPLP-ghacc/Marshall/releases) is a desktop application built on WPF (.NET 8) designed for managing and executing Python scripts in a modular, visual interface. Each script runs in an independent block, supporting features such as automatic restarts at configurable intervals, state persistence across sessions, and real-time output monitoring. The application serves as a tool for automating tasks and integrating Python scripts into a controlled environment.

![Marshall Preview](https://github.com/user-attachments/assets/37691958-c9a0-4328-b2aa-b1821bafd328)

## Key Features

- **Modular Blocks**: Create, rearrange, and remove blocks for individual Python scripts via drag-and-drop.
- **Looping Execution**: Enable automatic script restarts with user-defined intervals in seconds.
- **Automatic Module Installation**: Detects and installs missing Python modules using `pip` during runtime.
- **Interactive Input**: Supports real-time text input for scripts requiring user interaction.
- **Process Management**: Utilizes Job Objects for reliable process termination and cleanup.
- **State Persistence**: Automatically saves and restores script paths, loop settings, and block layouts.
- **System Tray Integration**: Provides a tray icon with a menu listing active scripts (upcoming enhancement).
- **Customizable Settings**: Includes resource limits (CPU/RAM), auto-start with Windows, font customization, window opacity, and more.
- **Additional Tools**: Automatic Python installation prompt, output copying, and error notifications.


## System Requirements

- **Operating System**: Windows 10/11 (64-bit)
- **Framework**: .NET 8.0 Runtime
- **Python**: 3.8+ (must be in PATH)
- **Permissions**: Administrative rights for `pip` package installations (for auto-module fixes)

  

