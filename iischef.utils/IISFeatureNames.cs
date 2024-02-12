namespace iischef.utils
{
    /// <summary>
    /// Contains constants for various IIS feature names.
    /// </summary>
    public static class IISFeatureNames
    {
        // Network and Security Features
        public const string IpRestrictions = "IIS-IPSecurity";
        public const string RequestFiltering = "IIS-RequestFiltering";
        public const string BasicAuthentication = "IIS-BasicAuthentication";
        public const string WindowsAuthentication = "IIS-WindowsAuthentication";
        public const string DigestAuthentication = "IIS-DigestAuthentication";
        public const string ClientCertificateMappingAuthentication = "IIS-ClientCertificateMappingAuthentication";
        public const string IISCertificateMappingAuthentication = "IIS-IISCertificateMappingAuthentication";
        public const string URLAuthorization = "IIS-URLAuthorization";

        // Performance Features
        public const string StaticContent = "IIS-StaticContent";
        public const string HttpCompressionStatic = "IIS-HttpCompressionStatic";
        public const string HttpCompressionDynamic = "IIS-HttpCompressionDynamic";
        public const string WebSockets = "IIS-WebSockets";

        // Application Development Features
        public const string ApplicationInit = "IIS-ApplicationInit";
        public const string AspNet = "IIS-ASPNET";
        public const string AspNet45 = "IIS-ASPNET45";
        public const string ISAPIExtensions = "IIS-ISAPIExtensions";
        public const string ISAPIFilters = "IIS-ISAPIFilter";
        public const string CGI = "IIS-CGI";
        public const string ServerSideIncludes = "IIS-ServerSideIncludes";

        // Management Tools
        public const string IisManagementConsole = "IIS-ManagementConsole";
        public const string IisManagementScriptingTools = "IIS-ManagementScriptingTools";
        public const string IisManagementService = "IIS-ManagementService";
        public const string Iis6ManagementCompatibility = "IIS-IIS6ManagementCompatibility";
        public const string Iis6ScriptingTools = "IIS-LegacyScripts"; // Assuming this refers to Legacy Scripts
        public const string Iis6WMICompatibility = "IIS-WMICompatibility";

        // Other Features
        public const string Metabase = "IIS-Metabase";
        public const string CentralCertificateStore = "IIS-CertProvider";
        public const string FtpService = "IIS-FTPServer";
        public const string HttpLogging = "IIS-HttpLogging";
        public const string CustomLogging = "IIS-CustomLogging";
        public const string OdbcLogging = "IIS-ODBCLogging";
        public const string DirectoryBrowsing = "IIS-DirectoryBrowsing";
        public const string HealthAndDiagnostics = "IIS-HealthAndDiagnostics";
        public const string Tracing = "IIS-HttpTracing"; // Assuming HttpTracing is what you meant by Tracing

        // Additional Features Based on Your List
        public const string WebServerRole = "IIS-WebServerRole";
        public const string WebServer = "IIS-WebServer";
        public const string CommonHttpFeatures = "IIS-CommonHttpFeatures";
        public const string HttpErrors = "IIS-HttpErrors";
        public const string HttpRedirect = "IIS-HttpRedirect";
        public const string ApplicationDevelopment = "IIS-ApplicationDevelopment";
        public const string Security = "IIS-Security";
        public const string NetFxExtensibility = "IIS-NetFxExtensibility";
        public const string NetFxExtensibility45 = "IIS-NetFxExtensibility45";
        public const string LoggingLibraries = "IIS-LoggingLibraries";
        public const string RequestMonitor = "IIS-RequestMonitor";
        public const string HttpTracing = "IIS-HttpTracing";
        public const string WebServerManagementTools = "IIS-WebServerManagementTools";
        public const string IIS6ManagementCompatibility = "IIS-IIS6ManagementCompatibility";
        public const string HostableWebCore = "IIS-HostableWebCore";
        public const string DefaultDocument = "IIS-DefaultDocument";
        public const string WebDAV = "IIS-WebDAV";
        public const string ASP = "IIS-ASP";
        public const string LegacySnapIn = "IIS-LegacySnapIn";
        public const string FTPSvc = "IIS-FTPSvc";
        public const string FTPExtensibility = "IIS-FTPExtensibility";
    }
}
