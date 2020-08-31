using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace iischef.utils
{
    public class XMLtoLINQutils
    {
        public void RemoveTags(string tagname, XElement element)
        {
            List<XElement> elems = new List<XElement>();
            foreach (XElement elem in element.DescendantsExtended(tagname))
            {
                elems.Add(elem);
            }

            foreach (XElement elem in elems)
            {
                elem.Remove();
            }
        }

        public void RemoveTagsWithCondition(string tagname, XElement element, Func<XElement, bool> condicion)
        {
            List<XElement> elems = new List<XElement>();
            foreach (XElement elem in element.DescendantsExtended(tagname))
            {
                if (condicion(elem))
                {
                    elems.Add(elem);
                }
            }

            foreach (XElement elem in elems)
            {
                elem.Remove();
            }
        }

        public void RemoveAllAttributes(string name, XElement parent)
        {
            var atts = (from s in parent.DescendantsAndSelf()
                        where s.Attribute(name) != null
                        select s.Attribute(name));
            foreach (XAttribute elem in atts)
            {
                elem.Remove();
            }
        }

        public void AddAttributeToElements(XElement parent, string type, string name, string value)
        {
            foreach (XElement elem in parent.DescendantsExtended(type))
            {
                if (elem.Attribute(name) != null)
                {
                    elem.Attribute(name).Remove();
                }

                XAttribute att = new XAttribute(name, value);
                elem.Add(att);
            }
        }

        public void AlterTagTextContents(XElement parent, string tagType, Func<string, string> modifier)
        {
            foreach (XElement elem in parent.DescendantsExtended(tagType))
            {
                var txt = from p in elem.DescendantNodes()
                          where p is XText
                          select p;
                if (txt.Count() != 0)
                {
                    XText el = ((XText)txt.First());
                    el.Value = modifier(el.Value);
                }
            }
        }

        public void ReplaceAllElementsWithContents(XElement elem, string type)
        {
            while (elem.DescendantsExtended(type).Count() > 0)
            {
                List<XElement> elems = new List<XElement>();
                foreach (XElement e in elem.DescendantsExtended(type))
                {
                    elems.Add(e);
                }

                foreach (XElement e in elems)
                {
                    this.ReplaceElementWithContents(e);
                }
            }
        }

        public void ReplaceAllElementsWithContentsCondition(XElement elem, string type, Func<XElement, bool> condicion)
        {
            while ((from p in elem.DescendantsExtended(type)
                    where condicion(p)
                    select p).Count() > 0)
            {
                List<XElement> elems = new List<XElement>();
                foreach (XElement e in elem.DescendantsExtended(type))
                {
                    elems.Add(e);
                }

                foreach (XElement e in elems)
                {
                    if (condicion(e))
                    {
                        this.ReplaceElementWithContents(e);
                    }
                }
            }
        }

        public void ReplaceTagsWithOtherTag(XElement elem, string originaltag, string destinationtag)
        {
            foreach (XElement e in elem.DescendantsAndSelfExtended(originaltag).ToList())
            {
                XElement destination = new XElement(destinationtag);
                List<XNode> nodos = new List<XNode>();
                foreach (XNode n in e.Nodes())
                {
                    nodos.Add(n);
                }

                foreach (XNode n in nodos)
                {
                    destination.Add(n);
                }

                e.AddBeforeSelf(destination);
                e.Remove();
            }
        }

        public void ReplaceElementWithContents(XElement elem)
        {
            try
            {
                List<XNode> nodos = new List<XNode>();
                foreach (XNode n in elem.Nodes())
                {
                    nodos.Add(n);
                }

                foreach (XNode n in nodos)
                {
                    elem.AddBeforeSelf(n);
                }

                elem.Remove();
            }
            catch 
            { 
            }
        }

        /// <summary>
        /// NOS DA EL ÚLTIMO PADRE DE LA CADENA DE HERENCIA QUE TIENE LOS MISMOS CONTENIDOS QUE EL NODO ACTUAL
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public HtmlAgilityPack.HtmlNode LastEquivalentContentParent(HtmlAgilityPack.HtmlNode node)
        {
            var parent = node;

            while (node.ParentNode != null)
            {
                if (node.ParentNode.InnerText.ToLower().Trim() == node.InnerText.Trim())
                {
                    parent = node.ParentNode;
                }
            }

            return parent;
        }

        public bool HasChildrenStructureWithIndependentContent(HtmlAgilityPack.HtmlNode node)
        {
            var parent = node;

            foreach (var n in node.DescendantsElements())
            {
                if (n.InnerText.Trim() != node.InnerText.Trim())
                {
                    return false;
                }

                if (this.HasChildrenStructureWithIndependentContent(n))
                {
                    return false;
                }
            }

            return true;
        }

        public void ReplaceElementWithContents(HtmlAgilityPack.HtmlNode elem)
        {
            try
            {
                List<HtmlAgilityPack.HtmlNode> nodos = new List<HtmlAgilityPack.HtmlNode>();
                foreach (HtmlAgilityPack.HtmlNode n in elem.ChildNodes)
                { 
                    nodos.Add(n); 
                }

                foreach (HtmlAgilityPack.HtmlNode n in nodos)
                { 
                    elem.ParentNode.InsertBefore(n, elem); 
                }

                elem.Remove();
            }
            catch 
            { 
            }
        }

        public void ReplaceTagsWithOtherTag(HtmlAgilityPack.HtmlNode elem, string originaltag, string destinationtag)
        {
            foreach (HtmlAgilityPack.HtmlNode e in elem.Descendants(originaltag).ToList())
            {
                e.Name = destinationtag;
            }
        }
    }
}
