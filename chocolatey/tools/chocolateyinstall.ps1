$ErrorActionPreference = 'Stop';

$packageName= 'iischef'
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

# Install the powershell module
$ZipPath = Join-Path $toolsDir '\iischef.cmdlet.zip'
$DESTINATION= Join-Path $env:ProgramFiles "\WindowsPowerShell\Modules\Chef"
New-Item -ItemType directory -Force -Path $DESTINATION
(new-object -com shell.application).namespace($DESTINATION).CopyHere((new-object -com shell.application).namespace($ZipPath).Items(),16)
Remove-Item $ZipPath