using iischef.utils;
using Microsoft.Win32;
using Xunit;

namespace iischeftests
{
    public class UtilsRegistryTests
    {
        [Fact]
        public void GetRegistryKeyValue64_ReturnsCorrectValue_WhenKeyExists()
        {
            // Arrange
            var hive = RegistryHive.LocalMachine;
            var key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            var valueName = "ProductName";
            var defaultValue = "Unknown";

            // Act
            var result = UtilsRegistry.GetRegistryKeyValue64(hive, key, valueName, defaultValue);

            // Assert
            Assert.NotEqual(defaultValue, result);
            Assert.IsType<string>(result);
        }

        [Fact]
        public void GetRegistryKeyValue64_ReturnsDefaultValue_WhenKeyDoesNotExist()
        {
            // Arrange
            var hive = RegistryHive.LocalMachine;
            var key = @"SOFTWARE\NonExistentKey";
            var valueName = "NonExistentValue";
            var defaultValue = "Unknown";

            // Act
            var result = UtilsRegistry.GetRegistryKeyValue64(hive, key, valueName, defaultValue);

            // Assert
            Assert.Equal(defaultValue, result);
        }

        [Fact]
        public void SetRegistryValue_SetsValueCorrectly_WhenKeyExists()
        {
            // Arrange
            var hive = RegistryHive.CurrentUser;
            var key = @"Software\TestKey";
            var name = "TestValue";
            var realValue = "Test";
            var valueKind = RegistryValueKind.String;

            // Act
            UtilsRegistry.SetRegistryValue(hive, key, name, realValue, valueKind);
            var result = UtilsRegistry.GetRegistryKeyValue64(hive, key, name, "Default");

            // Assert
            Assert.Equal(realValue, result);

            // Cleanup
            var view32 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
            var clsid32 = view32.OpenSubKey(key, true);
            clsid32?.DeleteValue(name, false);

            var view64 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            var clsid64 = view64.OpenSubKey(key, true);
            clsid64?.DeleteValue(name, false);
        }

        [Fact]
        public void SetRegistryValue_CreatesNewKeyAndSetsValue_WhenKeyDoesNotExist()
        {
            // Arrange
            var hive = RegistryHive.CurrentUser;
            var key = @"Software\NonExistentKey";
            var name = "NewValue";
            var realValue = "New";
            var valueKind = RegistryValueKind.String;

            // Act
            UtilsRegistry.SetRegistryValue(hive, key, name, realValue, valueKind);
            var result = UtilsRegistry.GetRegistryKeyValue64(hive, key, name, "Default");

            // Assert
            Assert.Equal(realValue, result);

            // Cleanup
            var view32 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
            var clsid32 = view32.OpenSubKey(key, true);
            clsid32?.DeleteValue(name, false);
            clsid32?.Dispose();
            view32.DeleteSubKey(key, false);

            var view64 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            var clsid64 = view64.OpenSubKey(key, true);
            clsid64?.DeleteValue(name, false);
            clsid64?.Dispose();
            view64.DeleteSubKey(key, false);
        }
    }
}