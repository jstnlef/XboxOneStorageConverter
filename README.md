# Xbox One Storage Converter

This repository now contains a macOS-first `.NET 10` CLI for toggling the MBR signature used by Xbox One external storage devices.

The original project was a Windows-only `.NET Framework 3.5` WinForms app. That code is still in the repo as reference, but the active solution now builds the modern CLI under `src/XboxOneStorageConverter.Cli`.

## What It Does

Xbox One external drives use a non-standard MBR signature:

- `99 CC` means the drive is in Xbox mode.
- `55 AA` means the drive is in PC mode.

This tool scans external physical disks on macOS with `diskutil`, reads sector 0 from the raw disk device, and toggles those two signature bytes when the disk already looks like an Xbox-formatted drive.

## Requirements

- macOS
- .NET SDK 10
- `sudo` for raw disk inspection or writes

## Build

```bash
dotnet build "XBOX One Drive Converter.sln"
```

## Usage

List eligible external disks:

```bash
dotnet run --project src/XboxOneStorageConverter.Cli -- scan
```

Inspect one disk in detail:

```bash
sudo dotnet run --project src/XboxOneStorageConverter.Cli -- inspect disk4
```

Switch a disk back to PC mode:

```bash
sudo dotnet run --project src/XboxOneStorageConverter.Cli -- set-mode disk4 pc
```

Switch a disk back to Xbox mode:

```bash
sudo dotnet run --project src/XboxOneStorageConverter.Cli -- set-mode disk4 xbox
```

## Important Notes

- The tool only targets external physical whole disks. Internal disks and virtual disks are filtered out.
- The tool refuses to write when the MBR does not already look like an Xbox external storage device.
- `set-mode` unmounts the target disk before writing sector 0.
- Formatting a drive to NTFS is not implemented on macOS in this port. Prepare the filesystem separately, then use `set-mode`.

## Risk

This tool writes directly to sector 0 of a disk. If you point it at the wrong disk, you can destroy partition metadata and lose data.
