namespace iischef.utils
{
    /// <summary>
    /// After a certificate has been generated, all the available paths
    /// </summary>
    public class CertificatePaths
    {
        public string keyGenFile { get; set; }

        public string keyPemFile { get; set; }

        public string csrGenFile { get; set; }

        public string csrPemFile { get; set; }

        public string crtDerFile { get; set; }

        public string crtPemFile { get; set; }

        public string chainPemFile { get; set; }

        public string name { get; set; }

        public string pfxPemFile { get; set; }
    }
}
