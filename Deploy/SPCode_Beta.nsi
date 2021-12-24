!include "MUI2.nsh"
!include "DotNetChecker.nsh"
!include "FileAssociation.nsh"
!addplugindir .\nsis-plugins

Name "SPCode Beta"
OutFile "SPCode.Beta.Installer.exe"

InstallDir $APPDATA\spcodebeta

RequestExecutionLevel admin

!define SHCNE_ASSOCCHANGED 0x8000000
!define SHCNF_IDLIST 0

!define MUI_ABORTWARNING
!define MUI_ICON "icon.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "GPLv3.txt"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Program" prog01
SectionIn 1 RO
SetOutPath $INSTDIR

!insertmacro CheckNetFramework 48

File "SPCode Beta.exe"
File MahApps.Metro.dll
File ICSharpCode.AvalonEdit.dll
File System.Windows.Interactivity.dll
File Xceed.Wpf.AvalonDock.dll
File Xceed.Wpf.AvalonDock.Themes.Metro.dll
File smxdasm.dll
File ValveQuery.dll
File MdXaml.dll
File SourcepawnCondenser.dll
File ByteSize.dll
File Renci.SshNet.dll
File Newtonsoft.Json.dll
File DiscordRPC.dll
File ControlzEx.dll
File Octokit.dll
File Microsoft.WindowsAPICodePack.dll
File Microsoft.WindowsAPICodePack.Shell.dll

File lang_0_spcode.xml
File License.txt
File GPLv3.txt

CreateDirectory "$APPDATA\spcodebeta\crashlogs"
CreateDirectory "$APPDATA\spcodebeta\lysis"
CreateDirectory "$APPDATA\spcodebeta\sourcepawn"
CreateDirectory "$APPDATA\spcodebeta\sourcepawn\errorfiles"
CreateDirectory "$APPDATA\spcodebeta\sourcepawn\temp"
CreateDirectory "$APPDATA\spcodebeta\sourcepawn\templates"
CreateDirectory "$APPDATA\spcodebeta\sourcepawn\configs"
CreateDirectory "$APPDATA\spcodebeta\sourcepawn\configs\sm_1_10_0_6509"

SetOutPath $APPDATA\spcodebeta
File /r ".\sourcepawn"
File /r ".\lysis"

IfFileExists $APPDATA\spcodebeta\options_0.dat OptionsExist OptionsDoesNotExist
OptionsExist:
Delete $APPDATA\spcodebeta\options_0.dat
OptionsDoesNotExist:

WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" DisplayName" "SPCode Beta - A lightweight SourcePawn editor"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" UninstallString" "$INSTDIR\uninstall.exe"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" "InstallLocation" "$INSTDIR"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" "DisplayIcon" "$INSTDIR\SPCode Beta.exe"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" "Publisher" "SPCode Organization"
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" "NoModify" 1
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta" "NoRepair" 1

WriteUninstaller $INSTDIR\uninstall.exe
SectionEnd


Section "File Association (.sp)" prog02
SectionIn 1
${registerExtension} "$INSTDIR\SPCode.exe" ".sp" "SourcePawn Script"
System::Call 'Shell32::SHChangeNotify(i ${SHCNE_ASSOCCHANGED}, i ${SHCNF_IDLIST}, i 0, i 0)'
SectionEnd


Section "File Association (.inc)" prog03
SectionIn 1
${registerExtension} "$INSTDIR\SPCode.exe" ".inc" "SourcePawn Include"
System::Call 'Shell32::SHChangeNotify(i ${SHCNE_ASSOCCHANGED}, i ${SHCNF_IDLIST}, i 0, i 0)'
SectionEnd


Section "File Association (.smx)" prog04
SectionIn 1
${registerExtension} "$INSTDIR\SPCode.exe" ".smx" "SourceMod Plugin"
System::Call 'Shell32::SHChangeNotify(i ${SHCNE_ASSOCCHANGED}, i ${SHCNF_IDLIST}, i 0, i 0)'
SectionEnd


Section "Desktop Shortcut" prog05
SectionIn 1
CreateShortCut "$DESKTOP\SPCode Beta.lnk" "$INSTDIR\SPCode Beta.exe" ""
SectionEnd

Section "Startmenu Shortcut" prog06
SectionIn 1
CreateShortCut "$SMPROGRAMS\SPCode Beta.lnk" "$INSTDIR\SPCode.exe" ""
SectionEnd

Section "Uninstall"

Delete $INSTDIR\uninstall.exe
Delete "$INSTDIR\SPCode Beta.exe"
Delete $INSTDIR\MahApps.Metro.dll
Delete $INSTDIR\ICSharpCode.AvalonEdit.dll
Delete $INSTDIR\System.Windows.Interactivity.dll
Delete $INSTDIR\Xceed.Wpf.AvalonDock.dll
Delete $INSTDIR\Xceed.Wpf.AvalonDock.Themes.Metro.dll
Delete $INSTDIR\smxdasm.dll
Delete $INSTDIR\LysisForSpedit.dll
Delete $INSTDIR\ValveQuery.dll
Delete $INSTDIR\SourcepawnCondenser.dll
Delete $INSTDIR\Renci.SshNet.dll
Delete $INSTDIR\Newtonsoft.Json.dll
Delete $INSTDIR\MdXaml.dll
Delete $INSTDIR\DiscordRPC.dll
Delete $INSTDIR\Microsoft.WindowsAPICodePack.dll
Delete $INSTDIR\Microsoft.WindowsAPICodePack.Shell.dll

Delete $INSTDIR\lang_0_spcode.xml
Delete $INSTDIR\License.txt
Delete $INSTDIR\GPLv3.txt
Delete $INSTDIR\*.dat
RMDir /r $APPDATA\spcodebeta
RMDir $INSTDIR

Delete "$DESKTOP\SPCode Beta.lnk"
Delete "$SMPROGRAMS\SPCode Beta.lnk"


DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\spcodebeta"

${unregisterExtension} ".sp" "Sourcepawn Script"
${unregisterExtension} ".inc" "Sourcepawn Include-File"
${unregisterExtension} ".smx" "Sourcemod Plugin"
System::Call 'Shell32::SHChangeNotify(i ${SHCNE_ASSOCCHANGED}, i ${SHCNF_IDLIST}, i 0, i 0)'
 
SectionEnd