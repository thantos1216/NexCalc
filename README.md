# NexCalc

NexCalc is a C# calculator for Windows and Android with a shared calculation engine. The Windows app uses WPF and includes scientific, graphing, programmer, and Python modes.

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
