# NexCalc

NexCalc is a C# calculator for Windows and Android with a shared calculation engine. Both apps include Standard, Scientific, Programmer, Graph, Statistics, Carpenter, Unit Converter, and Python modes.

## Downloads

Get the [Windows x64 installer and Android APK from GitHub Releases](https://github.com/thantos1216/NexCalc/releases/latest).

## Windows (WPF)

On Windows with the .NET 10 SDK installed, run:

```powershell
dotnet build NexCalc.slnx
dotnet run --project src/NexCalc
```

The **Menu** switches modes. In Scientific mode, **Shift** reveals alternate
functions for the next alternate keypress. **Norm** displays the exact fraction
alongside a rounded decimal when applicable. Use **Number pad** in Graph mode to
enter expressions or adjust plot settings.

Carpenter supports Imperial and Metric length, area, volume, board feet, diagonals,
and material quantity. The Unit Converter covers length, mass, volume, area,
temperature, and speed. Both modes accept fractions and expressions.

Windows Python mode uses the locally installed Python interpreter on PATH and
provides a resizable output pane. Android bundles its own Python runtime.

### Build the Windows installer

Install Inno Setup 6. From PowerShell at the repository root:

```powershell
dotnet publish src/NexCalc -c Release -r win-x64 --self-contained true -o release/app
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/NexCalc.iss
```

Use the path to your installed copy of ISCC if it differs. The installer is
written to the `installer` directory.

## Run the shared tests

```powershell
dotnet run --project src/NexCalc.Tests
```

## License

NexCalc is available under the [MIT License](LICENSE.txt).

## Android 0.2.7

NexCalc runs on Android 6.0 (API 23) and later. This release has Android
version code 9 and uses the Git tag `android-v0.2.7`.

- **Standard** is first in the menu. The app opens in Scientific mode.
- **Scientific** includes a Shift key for alternate functions and can show exact
  fractions alongside rounded results.
- **Carpenter** has an on-screen keypad for whole numbers, decimals, and mixed
  fractions. Choose Imperial or Metric for length, area, volume, board feet,
  diagonals, and material quantity. Metric length accepts mm, cm, and m.
- **Programmer, Graph, Stat, and Unit Converter** are also available.
- **Python** runs offline with bundled Pyodide. The scrollable output pane
  grows when the keyboard closes. Each run starts a fresh interpreter with the
  included standard library.

### Build the Android APK

Install the .NET 10 SDK with the Android workload, JDK 21, and the Android SDK.
The asset preparation script uses curl, sha256sum, and tar. From Bash:

```bash
dotnet workload install android
sdkmanager 'platforms;android-36.1' 'build-tools;36.0.0'
tools/prepare-android-python.sh
dotnet build src/NexCalc.Android -c Debug
```

The signed test APK is written to
`src/NexCalc.Android/bin/Debug/net10.0-android/`. The
[Android workflow](.github/workflows/android.yml) runs the core tests and uploads
an APK artifact on pushes to `android-port`. Android requires the same signing
certificate to install a build over an existing installation.
