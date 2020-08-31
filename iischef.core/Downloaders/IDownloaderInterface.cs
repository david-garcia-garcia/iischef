namespace iischef.core.Downloaders
{
    /// <summary>
    /// A downloader is a component in charge of pulling remote changes
    /// for an application.
    /// </summary>
    public interface IDownloaderInterface
    {
        /// <summary>
        /// In order to prevent re-deployment of same artifact.
        /// </summary>
        /// <param name="buildId">The build ID to search for.</param>
        /// <returns></returns>
        string GetNextId(string buildId);

        /// <summary>
        /// Pull an artifact using it's ID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="preferredLocalArtifactPath">If the downloader can pick a location for the local artifact, use this as a preferred destination.</param>
        /// <returns></returns>
        Artifact PullFromId(string id, string preferredLocalArtifactPath);
    }
}
