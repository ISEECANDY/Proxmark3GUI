# Quick Start - Console Logging Feature

## Build & Run

```powershell
cd C:\Users\home\Desktop\Proxmark3GUI
dotnet build
dotnet run
```

## Quick Test (No Device Needed)

1. Open the GUI
2. Go to **Commands** module (left sidebar)
3. Select any command from the list (e.g., "hf search")
4. Click **Execute Command** button
5. **Expected Result**: Command appears in console like:
   ```
   > hf search
   [placeholder result]
   ```

## Full Test (With Proxmark3 Device)

### Setup
1. Connect Proxmark3 to COM9 (or update port in settings)
2. Power on the device
3. Open the GUI

### Test Device Info
1. Go to **Device Info** module
2. Click **Refresh** button
3. **Expected in Console**:
   ```
   > hw version
   [firmware version info from device]
   > hw status
   [device status info]
   ```

### Test Antenna Operations
1. Go to **Antenna Tuning** module
2. Click **Start** button
3. **Expected in Console**:
   ```
   > hf tune
   [antenna measurement results]
   > lf tune
   [LF results]
   ```

### Test MIFARE Operations
1. Go to **Reader/Writer** module
2. Select "Read Block"
3. Enter:
   - Block: 0
   - Key: FFFFFFFFFFFF
4. Click **Execute** button
5. **Expected in Console**:
   ```
   > hf mf rdbl 0 A FFFFFFFFFFFF
   [block data from device]
   ```

### Test Card Emulation
1. Go to **Card Emulation** module
2. Select a profile or create one
3. Click **Start Emulation** button
4. **Expected in Console**:
   ```
   > hf mf sim -t 1 -u AABBCCDD
   [emulation started message]
   ```

### Test Key Recovery
1. Go to **Key Recovery** module
2. Select an attack type (e.g., "Hardnested")
3. Enter parameters (UID, known key)
4. Click **Start Attack** button
5. **Expected in Console**:
   ```
   > hf mf hardnested --uid AABBCCDD --key FFFFFFFFFFFF --keytype A
   [key recovery output]
   ```

## Features Enabled

✅ **Real-time Command Logging** - See every command sent to device
✅ **Response Display** - Device responses appear immediately in console
✅ **Error Tracking** - Error messages logged to console
✅ **Connection Status** - See when device connects/disconnects
✅ **Complete Visibility** - Full command/response transparency

## Troubleshooting

### Commands Don't Appear in Console
- Make sure device is selected/connected
- Check that button click works (no UI errors)
- Check MainWindow console area is visible

### Device Not Responding
- Verify COM port setting (default COM9)
- Check device is powered and connected
- Try unplugging/replugging USB cable
- Restart the GUI

### Build Errors
- Ensure .NET 8.0 SDK is installed
- Run: `dotnet restore`
- Run: `dotnet clean`
- Run: `dotnet build`

## Console Output Examples

### Successful Read
```
> hf mf rdbl 0 A FFFFFFFFFFFF
Block 0: AABBCCDDEEFF00112233445566778899
```

### Successful Emulation Start
```
> hf mf sim -t 1 -u AABBCCDD
Simulating MIFARE Classic 1K...
```

### Device Error
```
> hf mf info
Error: No card detected
```

### Connection Status
```
Connected to COM9 @ 460800 baud
Device: Proxmark3 RDV4 Standalone
Firmware: v4.16500
```

## What's New

- **10+ modules** now have console logging
- **30+ commands** properly tracked
- **Real-time feedback** on all operations
- **Complete audit trail** of device communications

## Next Steps

1. Test various modules to verify console output
2. Compare console output with device behavior
3. Use console output for debugging
4. Report any command syntax issues

---

**Console logging is ready to use!** 🎉
