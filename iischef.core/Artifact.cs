using System;

namespace iischef.core
{
    public class Artifact
    {
        /// <summary>
        /// The artifact ID is a string based ID that represents
        /// the current build/job/artifact in such a way that the 
        /// downloader that obtained it will be able to grab it again
        /// given that the downloader context is the same. I.e. in appveyor
        /// this would be only the buildId.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Artifacts are passed around (once downloaded) through a local
        /// path where the artifact is alread unzipped.
        /// </summary>
        public string localPath { get; set; }

        /// <summary>
        /// When was this artifact obtained.
        /// </summary>
        public DateTime? obtainedAt { get; set; }

        /// <summary>
        /// If this is a remote/temporary artifact,
        /// so that it can be moved around or deleted upon finish.
        /// </summary>
        public bool isRemote { get; set; }

        /// <summary>
        /// Artifact settings...
        /// </summary>
        public ArtifactSettings artifactSettings { get; set; }

        /// <summary>
        /// Can be used by artifacts to store metadata...
        /// </summary>
        public object artifactMetadata { get; set; }
    }
}
