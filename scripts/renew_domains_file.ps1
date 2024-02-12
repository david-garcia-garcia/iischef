# Define the path to the domains.txt file
$mypath = $MyInvocation.MyCommand.Path
$mypath = Split-Path $mypath -Parent
$domainsFile = "$mypath\domains.txt"

# Check if the domains file exists
if (Test-Path $domainsFile) {
    # Read each line (hostname) from the domains.txt file
    Get-Content $domainsFile | ForEach-Object {
        $hostname = $_

        # Execute the Invoke-IISChefGetCert command for each hostname
        Invoke-IISChefGetCert -HostName $hostname -Provider Acme -RegistrationMail "tecnologia@etg.global" -RenewThresholdDays 20 -VerboseOut

        Write-Host "Processed hostname: $hostname"
    }
} else {
    Write-Host "The file $domainsFile does not exist."
}
