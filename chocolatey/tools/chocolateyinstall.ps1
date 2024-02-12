$ErrorActionPreference = 'Stop';

$packageName= 'iischef'
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

# Install the powershell module
$ZipPath = Join-Path $toolsDir '\iischef.cmdlet.zip'
$DESTINATION= Join-Path $env:ProgramFiles "\WindowsPowerShell\Modules\Chef"
New-Item -ItemType directory -Force -Path $DESTINATION
Get-ChocolateyUnzip -FileFullPath $ZipPath -Destination $DESTINATION
Remove-Item $ZipPath