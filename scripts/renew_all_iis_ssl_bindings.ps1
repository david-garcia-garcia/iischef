# Ensure the IISAdministration module is imported
Import-Module IISAdministration

# Iterate through all sites in IIS
Get-IISSite | ForEach-Object {
    $siteName = $_.Name
    $siteState = $_.State

    # Proceed only if the site is not in a stopped state
    if ($siteState -ne 'Stopped') {
        # Iterate through each binding of the current site
        $_.Bindings | ForEach-Object {
            $binding = $_

            # Check if the binding is HTTPS
            if ($binding.Protocol -eq 'https') {
                # Extract the domain name from the binding information
                $domainName = $binding.BindingInformation.Split(':')[2]

                # Check if the domain name is not empty and does not contain a wildcard
                if (![string]::IsNullOrWhiteSpace($domainName)) {
                    # Check if the domain name does not contain an asterisk
                    if (-not $domainName.Contains("*")) {
                        Invoke-IISChefGetCert -HostName $domainName -Provider Acme -RegistrationMail "tecnologia@mycompany.com" -RenewThresholdDays 20 -VerboseOut
                    }
                }
            }
        }
    }
}
