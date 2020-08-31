using HtmlAgilityPack;
using System;
using System.Linq;

namespace iischef.core.IIS
{
    /// <summary>
    /// 
    /// </summary>
    public class UtilsHtml
    {
        /// <summary>
        /// Prepend CDN 
        /// </summary>
        /// <param name="html"></param>
        /// <param name="cdnUri"></param>
        /// <returns></returns>
        public string PrependCdnToUri(string html, string cdnUri)
        {
            // ***************************************************************************
            // TODO: NO SE RESPETA EL ORDEN ORIGINAL DE LOS SCRIPTS, LINKS Y STYLES AL 100% CUANDO SE RECONSTRUYE EL BODY
            // ESTO PUEDE SER UN PROBLEMA CON LOS SCRIPTS
            // ***************************************************************************
            html = "<container>" + html + "</container>";

            // LOS FORMULARIOS NO DEBERÍAN AUTO CERRARSE, ES UN BUG NO CORREGIDO
            HtmlNode.ElementsFlags.Remove("form");

            HtmlDocument document = new HtmlDocument();

            document.OptionCheckSyntax = true;
            document.OptionFixNestedTags = true;
            document.OptionWriteEmptyNodes = true;
            document.OptionOutputOriginalCase = true;
            document.OptionWriteEmptyNodes = true;
            document.LoadHtml(html);

            this.RedirectTag(document, "link", "href", cdnUri);
            this.RedirectTag(document, "img", "src", cdnUri);
            this.RedirectTag(document, "script", "href", cdnUri);

            // Devolvemos el documento entero!
            return document.DocumentNode.Descendants("container").First().InnerHtml;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        /// <param name="tag"></param>
        /// <param name="attribute"></param>
        /// <param name="cdnPrefix"></param>
        protected void RedirectTag(HtmlDocument document, string tag, string attribute, string cdnPrefix)
        {
            var nodes = document.DocumentNode.Descendants(tag);

            if (nodes?.Any() != true)
            {
                return;
            }

            foreach (HtmlNode node in nodes)
            {
                var att = node.Attributes[attribute];
                if (att == null)
                {
                    continue;
                }

                // If we are unable to parse the original URI, do nothing!
                if (!Uri.TryCreate(att.Value, UriKind.RelativeOrAbsolute, out var attUri))
                {
                    continue;
                }

                // Nothing to do with an absolute URI
                if (attUri.IsAbsoluteUri)
                {
                    continue;
                }

                // Prepend CDN
                att.Value = cdnPrefix + "/" + attUri.ToString();
            }
        }
    }
}
