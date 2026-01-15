# SVS-SardineHead

## Table of Contents

- [Prerequisites](#prerequisites)
  - [Aicomi](#aicomi)
  - [SamabakeScramble](#samabakescramble)
  - [DigitalCraft](#digitalcraft-standalone)
- [Installation](#installation)
- [Migration between versions](#migration-between-versions)
- [How to Use](#how-to-use)
- [Known Issues](#known-issues)

Runtime texture and material modifier tool for Aicomi and SamabakeScramble and modification loader for DigitalCraft.

Binary releases contain required [Windows Forms](https://github.com/dotnet/winforms) distributed under MIT license.

## Prerequisites

### Aicomi

Confirmed working under Aicomi 1.0.7.

- [AC-HF_Patch](https://github.com/ManlyMarco/AC-HF_Patch)
  - Message Center
  - BepInEx.ConfigurationManager
  - SVS_BepisPlugins
- [Fishbone/CoastalSmell](https://github.com/MaybeSamigroup/SVS-Fishbone)
  - 4.0.0/2.0.0 or later

### SamabakeScramble

Confirmed working under SamabakeScramble 1.1.6

- [SVS-HF_Patch](https://github.com/ManlyMarco/SVS-HF_Patch)
  - Message Center
  - BepInEx.ConfigurationManager
  - SVS_BepisPlugins
- [Fishbone/CoastalSmell](https://github.com/MaybeSamigroup/SVS-Fishbone)
  - 4.0.0/2.0.0 or later

### DigitalCraft Standalone

Confirmed working under DigitalCraft 3.0.0.

- [BepInEx](https://github.com/BepInEx/BepInE)
  - [Bleeding Edge (BE) build](https://builds.bepinex.dev/projects/bepinex_be) #752 or later
- [Fishbone/CoastalSmell](https://github.com/MaybeSamigroup/SVS-Fishbone)
  - 4.0.0/2.0.0 or later

## Installation

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-SardineHead/releases/latest) to your game install directory.

## Migration between versions

### Migration from older releases

Remove SardineHead.dll from BepInEx/plugins.

Plugin assembly names are now SVS_SardineHead.dll and DC_SardineHead.dll.

These directories contained in previous releases are no longer used. Please delete them.

(GameRoot)/UserData/plugins/SamabakeScramble.SardineHead/chara
(GameRoot)/UserData/plugins/SamabakeScramble.SardineHead/default
(GameRoot)/UserData/plugins/SamabakeScramble.SardineHead/textures

### Migration from 1.X.X to 2.X.X

These directories contained in previous releases are no longer used.
Please move its contents to new one and delete it.

- (GameRoot)/UserData/plugins/SamabakeScramble.SardineHead
  - New! (GameRoot)/UserData/plugins/SardineHead

## How to use

Start character creation and use keyboard shortcut to show / hide the UI.

Ctrl+S is mapped as default and can be configured through plugin settings.

To save your modifications, you have to check the desired property.

## Known issues

To edit integer/float/vector input fields, you should check it for saving,

or actual material values are reflected in short period and can't modify.

Currently, modification UI is available in Character Creation only.

Currently, conflicts with [HC_FXsettings](https://github.com/TonWonton/HC_FXsettings/tree/DigitalCraft) in DC, will crash on character or scene load if both plugins loaded.
