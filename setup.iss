[Setup]
AppName=Screen Recorder
AppVersion=1.0.0
DefaultDirName={autopf}\Screen Recorder
DefaultGroupName=Screen Recorder
UninstallDisplayIcon={app}\ScreenRecorder.exe
SetupIconFile=Assets\avalonia-logo.ico
Compression=lzma2
SolidCompression=yes
OutputDir=.
OutputBaseFilename=ScreenRecorderSetup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Screen Recorder"; Filename: "{app}\ScreenRecorder.exe"
Name: "{commondesktop}\Screen Recorder"; Filename: "{app}\ScreenRecorder.exe"

[Run]
Filename: "{app}\ScreenRecorder.exe"; Description: "Launch Screen Recorder"; Flags: nowait postinstall skipifsilent
