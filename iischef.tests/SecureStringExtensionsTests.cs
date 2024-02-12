using iischef.core.IIS;
using iischef.utils;
using System;
using Xunit;

namespace iischeftests
{
    /// <summary>
    /// 
    /// </summary>
    public class SecureStringExtensionsTests
    {
        [Theory]
        [InlineData("*.example.com", true)]
        [InlineData("*.subdomain.example.com", true)]
        [InlineData("example.com", true)]
        [InlineData("subdomain.example.com", true)]
        [InlineData("*example.com", false)] // Invalid, asterisk not followed by dot
        [InlineData("*.example", false)] // Invalid, TLD too short
        [InlineData("example.*.com", false)] // Invalid, asterisk in the wrong position
        [InlineData("example..com", false)] // Invalid, double dot
        [InlineData("*.com", false)] // Invalid, missing domain name
        [InlineData("example", false)] // Invalid, missing TLD
        [InlineData("", false)] // Invalid, empty string
        [InlineData("*", false)] // Invalid, empty string
        [InlineData(null, false)] // Invalid, null
        public void ValidateHostnameTests(string hostname, bool expectedResult)
        {
            var result = SslBindingSync.ValidateHostnameInSslBinding(hostname);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("example.com", true)]
        [InlineData("sub.domain.co.uk", true)]
        [InlineData("localhost", true)]
        [InlineData("9to5host.com", true)]
        [InlineData("-invalid.com", false)]
        [InlineData("invalid-.com", false)]
        [InlineData("invalid.com-", false)]
        [InlineData("invalid.-com", false)]
        [InlineData("in..valid.com", false)]
        [InlineData("*.hosting.com", false)]
        [InlineData("*.hosting.*.com", false)]
        [InlineData("too.long.hostname.that.exceeds.the.maximum.allowed.length.of.two.hundred.and.fifty.three.characters.for.a.hostname.com.and.fifty.three.characters.for.a.hostname.comtoo.long.hostname.that.exceeds.the.maximum.allowed.length.of.two.hundred.and.fifty.three.characters.for.a.hostname.com.and.fifty.three.characters.for.a.hostname.com", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void HostnameValidator_ShouldValidateCorrectly(string hostname, bool expectedValidity)
        {
            bool isValid = HostnameValidator.IsValidHostname(hostname);
            Assert.Equal(expectedValidity, isValid);
        }

        [Fact]
        public void TestHostnameDns()
        {
           Assert.True(UtilsIis.CheckHostnameDNS("www.google.com"));
           Assert.False(UtilsIis.CheckHostnameDNS("MYnon-existne-comain.doesnotexist.really.com"));
        }

        [Fact]
        public void Parse_EmailFormat_CorrectlyParsesUsernameAndDomain()
        {
            // Arrange
            var input = "username@domain.loc";

            // Act
            var parser = new DomainUserParser(input);

            // Assert
            Assert.Equal("username", parser.Username);
            Assert.Equal("domain.loc", parser.Domain);
        }

        [Fact]
        public void Parse_BackslashFormat_CorrectlyParsesUsernameAndDomain()
        {
            // Arrange
            var input = "DOMAIN\\username";

            // Act
            var parser = new DomainUserParser(input);

            // Assert
            Assert.Equal("username", parser.Username);
            Assert.Equal("DOMAIN", parser.Domain);
        }

        [Fact]
        public void Parse_UsernameOnly_SetsUsernameCorrectlyAndDomainIsEmpty()
        {
            // Arrange
            var input = "username";

            // Act
            var parser = new DomainUserParser(input);

            // Assert
            Assert.Equal("username", parser.Username);
            Assert.Equal(string.Empty, parser.Domain);
        }

        [Theory]
        [InlineData("DOMAIN\\username\\extra")]
        [InlineData("user@name@domain.loc")]
        public void Parse_InvalidFormat_ThrowsArgumentException(string input)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new DomainUserParser(input));
            Assert.Contains("Input format is incorrect", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_NullOrWhitespaceInput_ThrowsArgumentException(string input)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new DomainUserParser(input));
            Assert.Contains("Input cannot be null or whitespace", exception.Message);
        }
    }
}