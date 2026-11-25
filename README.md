# NexCalc

NexCalc is a C# scientific and graphing calculator for Windows built with WPF. It includes programmer calculations and a Python mode.

## Build and run

On Windows with the .NET 10 SDK installed, run:

```powershell
dotnet build NexCalc.slnx
dotnet run --project src/NexCalc
```

## Run the tests

```powershell
dotnet run --project src/NexCalc.Tests
```

## License

NexCalc is available under the [MIT License](LICENSE.txt).

## Android

The Android project shares the C# calculation engine. It includes Scientific,
Programmer, Graph, Python, and Stat modes. Scientific mode offers phone and
Windows keypad layouts. The display, angle, and programmer settings persist
between launches.

Build with the .NET 10 Android workload, JDK 21, and the Android SDK. From Bash:

```bash
tools/prepare-android-python.sh
dotnet build src/NexCalc.Android -c Debug
```

The signed test APK is in
`src/NexCalc.Android/bin/Debug/net10.0-android/`. The GitHub Actions workflow
also builds and uploads it.

Python uses bundled Pyodide and runs offline. Each run starts a fresh
interpreter with the included standard library. Desktop Python packages,
system processes, and Android file access are unavailable in the sandbox.
