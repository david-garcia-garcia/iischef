using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Xml.Linq;

namespace iischef.utils
{
    public static class XMLtoLINQUtilsExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="xpath"></param>
        /// <param name="elem"></param>
        /// <returns></returns>
        public static XElement GetAndEnsureXpath(this XElement elem, string xpath)
        {
            List<string> parts = Regex.Split(xpath, "/").ToList();
            foreach (var p in parts)
            {
                if (!elem.Elements(p).Any())
                {
                    elem.Add(new XElement(p));
                }

                elem = elem.Elements(p).First();
            }

            return elem;
        }

        public static IEnumerable<XNode> NodeFlattened(this XElement node, Type nodeType, Func<XNode, bool> condition)
        {
            List<XNode> result = new List<XNode>();

            foreach (var s in node.Descendants())
            { 
                result.AddRange(NodeFlattened(s, nodeType, condition)); 
            }

            foreach (var n in node.Nodes())
            {
                if (n.GetType() == nodeType
                    && condition(n))
                {
                    result.Add(n);
                }
            }

            return result;
        }

        public static XAttribute AttributeExtended(this XElement source, XName name)
        {
            return (from p in source.Attributes()
                    where p.Name.LocalName == name
                    select p).FirstOrDefault();
        }

        public static IEnumerable<XElement> ElementsExtended(this XElement source, XName name)
        {
            return (from p in source.Elements()
                    where p.Name.LocalName == name
                    select p);
        }

        public static IEnumerable<XElement> DescendantsExtended(this XElement source, XName name)
        {
            return (from p in source.Descendants()
                    where p.Name.LocalName.ToLower() == name.LocalName.ToLower()
                    select p);
        }

        public static XAttribute AtributeExtended(this XElement source, XName name)
        {
            return (from p in source.Attributes()
                    where p.Name.LocalName == name.LocalName.ToLower()
                    select p).FirstOrDefault();
        }

        public static IEnumerable<XElement> DescendantsExtended(this IEnumerable<XElement> source, XName name)
        {
            List<XElement> results = new List<XElement>();
            foreach (var r in source)
            {
                var r2 = (from p in r.Descendants()
                          where p.Name.LocalName.ToLower() == name.LocalName.ToLower()
                          select p);

                results.AddRange(r2);
            }

            return results;
        }

        public static IEnumerable<XElement> DescendantsAndSelfExtended(this XElement source, XName name)
        {
            var prefix = string.Empty;
            if (source.Document.Root.Attributes("xmlsn").Count() > 0)
            { 
                prefix = source.Document.Root.Attribute("xmlns").Value; 
            }

            return source.DescendantsAndSelf(name).Union(source.DescendantsAndSelf("{" + prefix + "}" + name.LocalName));
        }

        public static IEnumerable<XElement> ElementsExtended(this XDocument source, XName name)
        {
            return source.Root.ElementsExtended(name);
        }

        public static IEnumerable<XElement> DescendantsExtended(this XDocument source, XName name)
        {
            return source.Root.DescendantsExtended(name);
        }

        public static IEnumerable<XElement> DescendantsAndSelfExtended(this XDocument source, XName name)
        {
            return source.Root.DescendantsAndSelfExtended(name);
        }

        public static IEnumerable<XElement> ElementsAfterSelfExtended(this XElement source, XName name)
        {
            return source.ElementsAfterSelf(name).Union(source.ElementsAfterSelf("{http://www.w3.org/1999/xhtml}" + name));
        }

        public static IEnumerable<XElement> ElementsBeforeSelfExtended(this XElement source, XName name)
        {
            return source.ElementsBeforeSelf(name).Union(source.ElementsBeforeSelf("{http://www.w3.org/1999/xhtml}" + name));
        }

        public static string OutterXml(this XElement source)
        {
            var reader = source.CreateReader();
            reader.MoveToContent();
            return reader.ReadOuterXml();
        }

        public static string InnerXml(this XElement source)
        {
            var reader = source.CreateReader();
            reader.MoveToContent();
            return reader.ReadInnerXml();
        }

        public static XElement FirstParent(this XElement source, XName name)
        {
            XElement parent = source.Parent;
            while (parent != null)
            {
                if (parent.Name == name || parent.Name == "{http://www.w3.org/1999/xhtml}" + name)
                { 
                    return parent; 
                }

                parent = parent.Parent;
            }

            return null;
        }

        public static HtmlAgilityPack.HtmlNode FirstParent(this HtmlAgilityPack.HtmlNode source, XName name)
        {
            HtmlAgilityPack.HtmlNode parent = source.ParentNode;
            while (parent != null)
            {
                if (parent.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    if (parent.Name == name || parent.Name == "{http://www.w3.org/1999/xhtml}" + name)
                    { 
                        return parent; 
                    }
                }

                parent = parent.ParentNode;
            }

            return null;
        }

        public static HtmlAgilityPack.HtmlNode PreviousSiblingElement(this HtmlAgilityPack.HtmlNode source)
        {
            HtmlAgilityPack.HtmlNode parent = source.PreviousSibling;
            while (parent != null)
            {
                if (parent.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    return parent;
                }

                parent = parent.PreviousSibling;
            }

            return null;
        }

        public static string GetOutputHTML(this HtmlAgilityPack.HtmlDocument source)
        {
            StringWriter stream = new StringWriter();
            source.Save(stream);
            return stream.ToString();
        }

        public static HtmlAgilityPack.HtmlNode NextSiblingElement(this HtmlAgilityPack.HtmlNode source)
        {
            HtmlAgilityPack.HtmlNode parent = source.NextSibling;
            while (parent != null)
            {
                if (parent.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    return parent;
                }

                parent = parent.NextSibling;
            }

            return null;
        }

        public static HtmlAgilityPack.HtmlNode NextSiblingElementWithoutText(this HtmlAgilityPack.HtmlNode source)
        {
            HtmlAgilityPack.HtmlNode parent = source.NextSibling;
            while (parent != null)
            {
                if (parent.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    return parent;
                }

                if (parent.InnerText.Trim() != string.Empty)
                {
                    return null;
                }

                parent = parent.NextSibling;
            }

            return null;
        }

        public static IEnumerable<HtmlAgilityPack.HtmlNode> DescendantsElements(this HtmlAgilityPack.HtmlNode source, string name = null)
        {
            return source.Descendants().Where((i) => i.NodeType == HtmlAgilityPack.HtmlNodeType.Element && (name != null ? i.Name == name : true));
        }

        public static HtmlAgilityPack.HtmlNode FirstPreviousSibling(this HtmlAgilityPack.HtmlNode source, XName name)
        {
            HtmlAgilityPack.HtmlNode parent = source.PreviousSibling;
            while (parent != null)
            {
                if (parent.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    if (parent.Name == name || parent.Name == "{http://www.w3.org/1999/xhtml}" + name)
                    {
                        return parent;
                    }
                }

                parent = parent.PreviousSibling;
            }

            return null;
        }

        public static HtmlAgilityPack.HtmlNode FirstNextSibling(this HtmlAgilityPack.HtmlNode source, XName name)
        {
            HtmlAgilityPack.HtmlNode parent = source.NextSibling;
            while (parent != null)
            {
                if (parent.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    if (parent.Name == name || parent.Name == "{http://www.w3.org/1999/xhtml}" + name)
                    {
                        return parent;
                    }
                }

                parent = parent.NextSibling;
            }

            return null;
        }

        public static XElement FirstParentWithCondition(this XElement source, XName name, Func<XElement, bool> condition)
        {
            XElement parent = source.Parent;
            while (parent != null)
            {
                if (parent.Name == name || parent.Name == "{http://www.w3.org/1999/xhtml}" + name)
                {
                    if (condition(parent))
                    {
                        return parent;
                    }
                }

                parent = parent.Parent;
            }

            return null;
        }
    }
}
