# SVS-SardineHead

Runtime texture and material modifier tool for SamabakeScramble and modification loader for DigitalCraft.

## Prerequisites

- [SVS-HF_Patch](https://github.com/ManlyMarco/SVS-HF_Patch)
  - Message Center
  - BepInEx.ConfigurationManager
  - SVS_BepisPlugins
- [Fishbone](https://github.com/MaybeSamigroup/SVS-Fishbone)
  - 2.1.0 or later

Confirmed working under SamabakeScramble 1.1.6 and DigitalCraft 2.1.0

## Installation

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-SardineHead/releases/latest) to your game install directory.

## Migration from older releases

Remove SardineHead.dll from BepinEx/plugins.

Plugin assembly names are now SVS_SardineHead.dll and DC_SardineHead.dll.

These directories contained in previous releases are no longer used.
Please delete it.

- (GameRoot)/UserData/plugins/SamabakeScramble.SardineHead/chara
- (GameRoot)/UserData/plugins/SamabakeScramble.SardineHead/default
- (GameRoot)/UserData/plugins/SamabakeScramble.SardineHead/textures

## How to use

Start character creation and use keyboard shortcut to show / hide the ui.

Ctrl + s is mapped as default and can be configured through plugin setting.

To save your modifications, you have to check desired property.

## Know issue

Material selection menu toggles funny,

exclusive selection is intended but under unknown condition, multiple selection can made.

To edit integer/float/vector input field, you should check it for saving,

or actual material values are reflected in short period and can't modify.

Currently, modification ui is available in SVS Character Creation only.

Currently, conflict with [HC_FXsettings](https://github.com/TonWonton/HC_FXsettings/tree/DigitalCraft) in DC, will crash on character or scene load if both plugins loaded.
