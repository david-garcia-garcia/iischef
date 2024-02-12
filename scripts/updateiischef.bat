mkdir %CD%\cmdlet
powershell -command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; (New-Object Net.WebClient).DownloadFile('https://ci.appveyor.com/api/projects/David19767/iischef/artifacts/iischef.cmdlet.zip?branch=3.x','%CD%\chef_cmdlet.zip')"
powershell -command "(new-object -com shell.application).namespace('%CD%\cmdlet').CopyHere((new-object -com shell.application).namespace('%CD%\chef_cmdlet.zip').Items(),16)"
set DESTINATION=%ProgramFiles%\WindowsPowerShell\Modules\iischef
mkdir "%DESTINATION%"
powershell -command "(new-object -com shell.application).namespace('%DESTINATION%').CopyHere((new-object -com shell.application).namespace('%CD%\chef_cmdlet.zip').Items(),16)"
del %CD%\chef_cmdlet.zip