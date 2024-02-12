using iischef.utils;
using System;
using System.IO;
using Xunit;

namespace iischeftests
{
    public class CompressionTests : IClassFixture<ChefTestFixture>
    {
        protected ChefTestFixture Fixture;

        public CompressionTests(ChefTestFixture fixture)
        {
            this.Fixture = fixture;
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void TestIisGetConfigIsolationPath()
        {
            Assert.NotNull(UtilsIis.GetConfigIsolationPath());
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void Test7ZExtractAndPackFileWithPassword()
        {
            var file = Path.GetTempFileName();
            File.WriteAllText(file, "nothing to extract");

            var compressedFile = file + ".7z";
            CompressionUtils.CreateWith7Z(file, compressedFile, 9, password: "mypassword");

            Assert.ThrowsAny<Exception>(() =>
            {
                CompressionUtils.ExtractWith7Z(compressedFile, file + ".extracted", "badpassword");
            });

            CompressionUtils.ExtractWith7Z(compressedFile, Path.GetTempPath(), "mypassword");

            File.Delete(file);
            File.Delete(compressedFile);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void Test7ZDetectsFilure()
        {
            var file = Path.GetTempFileName();
            File.WriteAllText(file, "nothing to extract");
            Assert.ThrowsAny<Exception>(() => { CompressionUtils.ExtractWith7Z(file, Path.GetTempPath()); });
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void Test7ZExtractsOk()
        {
            var logger = new TestLogsLogger(this, nameof(this.Test7ZExtractsOk));
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            var destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(destination);
            CompressionUtils.ExtractWith7Z(sampleArtifact, destination);

            Assert.True(Directory.Exists(Path.Combine(destination, "chef")));

            UtilsSystem.DeleteDirectory(destination, logger);
        }

        [Fact]
        [Trait("Category", "Functional")]
        public void Test7ZExtractsAndPacks()
        {
            var logger = new TestLogsLogger(this, nameof(this.Test7ZExtractsAndPacks));
            var sampleArtifact = UtilsSystem.GetResourceFileAsPath("samples/sample-php-artifact.zip");

            var destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(destination);
            CompressionUtils.ExtractWith7Z(sampleArtifact, destination);
            Assert.True(Directory.Exists(Path.Combine(destination, "chef")));

            var targetPath = Path.GetTempFileName();
            File.Delete(targetPath);
            CompressionUtils.CreateWith7Z(destination, targetPath);
            UtilsSystem.DeleteDirectory(destination, logger);

            Directory.CreateDirectory(destination);
            CompressionUtils.ExtractWith7Z(targetPath, destination);
            Assert.True(Directory.Exists(Path.Combine(destination, "chef")));

            UtilsSystem.DeleteDirectory(destination, logger);
        }
    }
}
