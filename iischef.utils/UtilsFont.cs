using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace iischef.utils
{
    public class UtilsFont
    {
        /// <summary>
        /// Given a directory with fonts, installs all fonts in the directory
        /// </summary>
        /// <param name="path"></param>
        public void InstallFont(string path)
        {
            string fontDestination = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            foreach (var file in Directory.EnumerateFiles(path, "*.otf"))
            {
                var faces = Fonts.GetTypefaces("file:///" + file).ToList();
                var face = faces.First();
                
                List<string> fontNameParts = new List<string>();

                // Build the font name...
                bool trueType = Path.GetExtension(file)?.Equals(".otf", StringComparison.CurrentCultureIgnoreCase) == true;
                string familyName = face.FontFamily.ToString().Split('#')[face.FontFamily.ToString().Split('#').Count() - 1];

                fontNameParts.Add(familyName);
                fontNameParts.Add(face.FaceNames.First().Value);

                if (trueType)
                {
                    fontNameParts.Add("(TrueType)");
                }

                string fontName = string.Join(" ", fontNameParts.Where((i) => !string.IsNullOrWhiteSpace(i)));

                var existingFontRegistryKey = UtilsRegistry.GetRegistryKeyValue32(
                    RegistryHive.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
                    fontName,
                    null);

                // Si la fuenta ya existe, no hacemos nada.
                if (existingFontRegistryKey != null)
                {
                    continue;
                }

                RegisterFont(file, fontName, fontDestination);
            }
        }

        /// <summary>
        /// Installs font on the user's system and adds it to the registry so it's available on the next session
        /// Your font must be included in your project with its build path set to 'Content' and its Copy property
        /// set to 'Copy Always'
        /// </summary>
        /// <param name="fontFilePath">Your font to be passed as a resource (i.e. "myfont.tff")</param>
        /// <param name="fontName"></param>
        /// <param name="fontDestination"></param>
        private static void RegisterFont(string fontFilePath, string fontName, string fontDestination)
        {
            string fontFileName = Path.GetFileName(fontFilePath);

            // Creates the full path where your font will be installed
            string fontFileDestinationSystemPath = Path.Combine(fontDestination, fontFileName);

            if (!File.Exists(fontFileDestinationSystemPath))
            {
                // Copies font to destination
                File.Copy(fontFilePath, fontFileDestinationSystemPath);
            }

            UtilsRegistry.SetRegistryValue(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
                fontName,
                fontFileName,
                RegistryValueKind.String);
        }
    }
}
