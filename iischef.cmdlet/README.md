﻿## Debugging instructions

Debugging settings are stored per-user, so it cannot be commited to the repository.

These are the relevant settings to debug a Cmdlet:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'">
    <StartAction>Program</StartAction>
    <StartProgram>C:\WINDOWS\system32\WindowsPowerShell\v1.0\powershell.exe</StartProgram>
    <StartArguments>-noexit -command "Import-Module %27.\iischef.dll%27"</StartArguments>
  </PropertyGroup>
</Project>
```