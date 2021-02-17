using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WiktionaryUtil
{
    internal class EnFiPage   : WiktionaryPage, IOutItem
    {

        private readonly IElement[] elements;
        private readonly IElement headline;
        internal readonly EnFiPageEntry[] entries;

        internal EnFiPage(string page) : base(page)
        {

            //     Unlike the Finnish Wiktionary, the English-language Wiktionary
            // does not have a consistent structure for its information 
            // across terms.  For expedience, I more-or-less bolted the
            // FiFiPage structure onto the EnFiPage, but a refactor might be
            // appropriate. 
            //     Nothing can be assumed about the data from its structure,
            // nor anything about the structure across terms.  It's often consistent, but
            // there are enough divergences that it will violate any assumptions at some point.
            //     For some examples, look at 
            //          -mme (2 entries, same category, "Suffix"; usually there is only one of each category on a page.)
            //          -nsä (extra usage note immediately after the term header; usually there is nothing like that) i.e. "(appended to a word that includes..."
            //          -nne (under definition #1 there is *another ordered list*, each item with examples; usually, a definition does not have a sub-list of definitions)
            //     I think the best approach is, rather than Entry, Section and Item, which was helpful
            // for the Finnish-language Wiktionary pages, to do away with "Entry", and 
            // combine Entry, Item and Section into a single class, Section, with
            // fields "header", "text", "sections".  Item could then be a "Section" with neither 
            // header nor sections. A section accumulates information until it finds a 'header',
            // then creates a new section.  The "Section" class could have a "role" field, that might contain
            // values like "example", "definition", "unknown", to help human parsing later.
            //

            headline = QuerySelector("h2:has(span):contains('Finnish')");
            if (headline == null) return;

            elements = GetFinnishElements(headline, new IElement[0]);

            var orphanElements = elements.Skip(1).TakeWhile(elem => elem.TagName != "H3");

            if (orphanElements != null && orphanElements.Count() > 0)
            {
                // TODO: do something aobut this edge case. (q.v. "kuka")
                // Ideally, tack them in order just after the first H3 in elements.
                // for now, just note it
                Logger.Warn("enfi page {0} has {1} orphaned elements", term, orphanElements.Count());
            }

            entries = EnFiPageEntry.GetEntries(elements, new EnFiPageEntry[0]);
        }

        // returns true if elem is the end of the Finnish word entry of the page
        internal static bool IsEndOfPage(IElement elem)
        {
            if (elem == null) return true;
            if (elem.TagName == "H2" && !elem.TextContent.ToLower().Contains("finnish")) return true;
            if (elem.TagName == "NOSCRIPT") return true;


            return false;
        }

        // returns only those elements that contain information about the Finnish word on this page
        private static IElement[] GetFinnishElements(IElement elem, IElement[] elems)
        {
            if (IsEndOfPage(elem)) return elems;
            // skip the 'sister wikipedia' divs.
            if (elem.TagName == "DIV" && elem.ClassList.Contains("sister-wikipedia") || elem.ClassList.Contains("sister-project")) return GetFinnishElements(elem.NextElementSibling, elems);
            return GetFinnishElements(elem.NextElementSibling, AddElement(elems, elem));
        }

        // returns a new array IElement[] with IElement elem at the end
        private static IElement[] AddElement(IElement[] elems, IElement elem)
        {
            IElement[] newArray = new IElement[elems.Length + 1];
            elems.CopyTo(newArray, 0);
            newArray.SetValue(elem, elems.Length);
            return newArray;
        }

        // returns a new array string[] with string str at the end
        private static string[] AddString(string[] strArray, string str)
        {
            string[] newArray = new string[strArray.Length + 1];
            strArray.CopyTo(newArray, 0);
            newArray.SetValue(str, strArray.Length);
            return newArray;
        }

        // returns elements up to but not including an element with tag tagName or end of (finnish) page
        internal static IElement[] GetElementsUntil(string tagName, IElement elem, IElement[] elems = null)
        {
            if (elems == null) return GetElementsUntil(tagName, elem, new IElement[0]);
            if (IsEndOfPage(elem)) return elems;
            if (elem.TagName == tagName) return elems;

            return GetElementsUntil(tagName, elem.NextElementSibling, AddElement(elems, elem));
        }

        public static EnFiPage GetPage(string term)
        {
            try
            {
                string ePage = Wiktionary.GetTermPage(term, "en");
                return new EnFiPage(ePage);
            }
            catch (WebException e)
            {
                Console.WriteLine(e);
                var errorMessage = (e.Status == WebExceptionStatus.ProtocolError) ? String.Format("{0}:{1}", (int)((HttpWebResponse)e.Response).StatusCode, ((HttpWebResponse)e.Response).StatusDescription) : e.Message;
                var s = String.Format(WiktionaryPage.ErrorPage, errorMessage);
                return new EnFiPage(s);
            }
            catch (Exception e)
            {   //TODO: include response info.
                Console.WriteLine(e);

                var s = String.Format(WiktionaryPage.ErrorPage, e.Message);
                return new EnFiPage(s);
            }
        }

        public bool IsEmpty()
        {
            return String.IsNullOrEmpty(term);
        }

        public bool IsFinnish()
        {
            return headline != null;
        }

        internal class EnFiPageEntry : IOutItem
        {

            //internal readonly Definition[] definitions;

            //private readonly IElement[] elements;
            internal readonly string heading;
            internal readonly IEnumerable<ISectionItem> items;
            internal readonly IEnumerable<Definition> definitions;
            internal readonly IEnumerable<Section> sections;

            //internal readonly LinkDictionary links;
            //internal readonly Morpheme[] inflections;
            //private readonly DeclensionTable[] declensions; // usually only one.
            //private readonly VerbConjugationPage verbConjugation;



            internal EnFiPageEntry(IElement[] elements)
            {
                var head = elements.First();
                heading = head.FirstElementChild.TextContent.Trim();

                IElement elem = elements.Skip(1).First();
                IElement nextElem = elements.Skip(2).FirstOrDefault();

                var hasDefinitions = (elem.TagName == "P" && nextElem != null && nextElem.TagName == "OL");

                if (hasDefinitions)
                {
                    definitions = Definition.GetDefinitions(heading, elem.NextElementSibling);
                    items = Enumerable.Empty<ISectionItem>();
                }
                else
                {
                    // these are top level items associated with the entry itself
                    // typically 'pronunciation' or 'see also' have these.
                    items = Item.GetItems(elem);
                    definitions = Enumerable.Empty<Definition>();
                }


                // these are sub-categories within the page entry.
                sections = Section.GetSections(elem);


            }

            // end of section / entry
            internal static bool IsEnd(IElement elem)
            {
                return EnFiPage.IsEndOfPage(elem) || elem.TagName == "H3";
            }

            internal static EnFiPageEntry[] GetEntries(IElement[] elements, EnFiPageEntry[] entries)
            {
                // The English language wiktionary is annoying because it is inconsistent across terms.
                // If the term has one classificiation (e.g. just "Verbi" for "puhua") then its layout hierachy is different than
                // if the term has more than one classification (e.g. "Interjektio, Substantiivi, Verbi" for "voi").


                IElement elem = elements.FirstOrDefault();
                if (EnFiPage.IsEndOfPage(elem)) return entries;

                // page entries are headed by h3 tags
                // if element is not an h3 then 
                var entryHeaderTags = new string[] { "H3" };
                if (!entryHeaderTags.Contains(elem.TagName))
                {
                    return GetEntries(elements.Skip(1).ToArray(), entries);
                }


                // otherwise, create a new PageEntry, add it to entries, and continue.
                EnFiPageEntry entry = new EnFiPageEntry(elements);
                return GetEntries(elements.Skip(1).ToArray(), EnFiPageEntry.AddEntry(entries, entry));

            }



            // just throws an error
            private static void Invalidate(IElement[] elements)
            {
                if (elements.Length == 0) throw new ArgumentException("Empty element list");
            }

            public static EnFiPageEntry[] AddEntry(EnFiPageEntry[] pages, EnFiPageEntry page)
            {
                EnFiPageEntry[] newArray = new EnFiPageEntry[pages.Length + 1];
                pages.CopyTo(newArray, 0);
                newArray.SetValue(page, pages.Length);
                return newArray;
            }

            public void Output()
            {
                if (definitions != null)
                {
                    foreach (Definition def in definitions) def.Output();
                }
                else Console.Out.WriteLine("\nEntry: {0}", heading);
                if (items != null) foreach (IOutItem item in items) item.Output();
                if (sections != null) foreach (Section sec in sections) sec.Output();
            }

            public IModelObject GetJsonObject()
            {

                var category = definitions.Count() > 0 ? heading : "";

                return new EntryObject()
                {
                    definitions = definitions.Select(d => (DefinitionObject) d.GetJsonObject()),
                    sections = sections.Select(s => (SectionObject) s.GetJsonObject()),
                    category = category
                    
                };
            }
        }

        internal class Definition : IOutItem
        {
            internal readonly IEnumerable<Example> examples;
            private LinkDictionary linkDict;

            internal IEnumerable<Link> links
            {
                get
                {
                    return (linkDict == null) ? null : linkDict.links;
                }
            }
            internal readonly IEnumerable<DefinitionNote> notes;
            internal readonly string text;
            private readonly string type;
            private readonly int rank;

            internal Definition(string type, IElement li)
            {
                var elem = (IElement)li.Clone();
                this.type = type;

                // remove garbage - be specific
                var spanHQ = elem.QuerySelector("span.HQToggle");
                if (spanHQ != null)
                {
                    elem.RemoveChild(spanHQ);
                }
                //var ollaUl = elem.Children.Select()
                //if (ollaUl != null)
                //{
                //    elem.RemoveChild(ollaUl);
                //}

                var exampleList = elem.QuerySelectorAll("dl");
                if (exampleList != null  && exampleList.Count() > 1)
                {


                    examples = exampleList.SelectMany(dl => Example.GetExamples(dl));

                    // occasionally there will be a definition note that does not follow the above pattern
                    notes = DefinitionNote.GetNotes(exampleList.First());

                    foreach (IElement list in exampleList) if (elem.Children.Contains(list)) elem.RemoveChild(list);
                }
                else
                {
                    examples = Enumerable.Empty<Example>();
                    notes = Enumerable.Empty<DefinitionNote>();
                }

                text = Wiktionary.NormalizeText(elem.TextContent.Trim());
                linkDict = new LinkDictionary(elem.QuerySelectorAll("a").Where(a => a.HasAttribute("href")));

            }

            public Definition(string type, IElement li, int rank) : this(type, li)
            {
                this.rank = rank;
            }

            internal static Definition[] GetDefinitions(string type, IElement ol)
            {
                var list = ol.Children.Where(c => c.TagName == "LI");
                var defs = list.Select((li, rank) => new Definition(type, li, rank + 1));
                return defs.ToArray();
            }

            public void Output()
            {
                Console.WriteLine("Definition: [{0}] {1}", type, text);
                var fiLinks = linkDict.links.Where(l => (l.href.Contains("#Finnish")));
                if (fiLinks != null) foreach (Link link in fiLinks) link.Output();
                if (examples != null) foreach (Example example in examples) example.Output();
                if (notes != null) foreach (DefinitionNote note in notes) note.Output();


            }

            public IModelObject GetJsonObject()
            {
                IEnumerable<ExampleObject[]> exampleOjbs = examples.Select(ex => ex.GetJsonArray());
                return new DefinitionObject() { language = "en", examples = exampleOjbs, text = text, links = linkDict.GetJsonObject(), rank = rank };
            }

            internal class DefinitionNote
            {

                internal readonly string text;
                internal readonly LinkDictionary links;

                internal readonly string fi;
                internal readonly LinkDictionary fiLinks;
                internal readonly string en;
                internal readonly LinkDictionary enLinks;

                internal DefinitionNote(IElement dd)
                {
                    text = dd.TextContent.Trim();
                    links = new LinkDictionary(dd.QuerySelectorAll("a").Where(a => a.HasAttribute("href")));

                    if (text.Contains("="))
                    {
                        var split = text.Split('=');

                        fi = split.First().Trim();
                        var fiAnchors = links.links.Where(link => fi.Contains(link.content));
                        if (fiAnchors != null && fiAnchors.Count() > 0) fiLinks = new LinkDictionary(fiAnchors);

                        en = split.Last().Trim();
                        var enAnchors = links.links.Where(link => en.Contains(link.content));
                        if (enAnchors != null && enAnchors.Count() > 0) enLinks = new LinkDictionary(enAnchors);
                    }
                }


                internal static IEnumerable<DefinitionNote> GetNotes(IElement dl)
                {
                    var exElems = dl.Children.Where(e => !Example.IsExample(e));
                    return exElems.Select(dd => new DefinitionNote(dd));
                }

                public void Output()
                {
                    if (!String.IsNullOrEmpty(fi))
                    {
                        Console.WriteLine("          ○ {0}", text);
                        if (fiLinks != null) fiLinks.Output();

                    }
                    else
                    {
                        Console.WriteLine("          ○ {0}", text);
                        if (links != null) links.Output();
                    }
                }
            }

            internal class Example
            {
                internal readonly string fi;
                private readonly LinkDictionary fiLinkDict;

                internal IEnumerable<Link> links
                {
                    get
                    {
                        return fiLinkDict == null ? null : fiLinkDict.links;
                    }
                }


                internal readonly string nb;

                internal readonly string en;
                private readonly LinkDictionary enLinks;

                // Example sentences take these forms:
                // <dd><i>finnish sentence</i><dl>english sentence</dl></dd>
                // <dl><dd><ul><li>Finnish sentence<ul><li>English sentence</li></ul></li></ul></dd></dl>
                // <dd>finnish sentence<dl><dd>english sentence</dd></dl></dd>

                private static readonly Regex ex1 = new Regex(@"<dd>[^<]*<i>(?<fi>.+?)<\/i>(?<extra>.*)<dl>[^<]*<dd>(?<en>.+?)<\/dd>", RegexOptions.Singleline);
                private static readonly Regex ex2 = new Regex(@"<dd>[^<]*<ul>[^<]*<li>(?<fi>.+?)<ul>[^<]*<li>(?<en>.+?)<\/li>[^<]*<\/ul>[^<]*<\/li>[^<]*<\/ul>[^<]*<\/dd>", RegexOptions.Singleline);
                private static readonly Regex ex3 = new Regex(@"<dd>(?<fi>.+?)<dl><dd>(?<en>.+?)<\/dd><\/dl><\/dd>");

                internal Example(IElement dd)
                {
                    var ddHtml = dd.OuterHtml.Replace("\n", String.Empty);
                    var m = ex3.IsMatch(ddHtml) ? ex3.Match(ddHtml) : ex1.IsMatch(ddHtml) ? ex1.Match(ddHtml) : ex2.IsMatch(ddHtml) ? ex2.Match(ddHtml) : null;
                    if (m == null)
                    {
                        string message = String.Format("Unknown match attempt in Example element {0}" + dd.OuterHtml);
                        throw new Exception(message);
                    }

                    var fiElem = Wiktionary.ParseString(m.Groups["fi"].Value);
                    var fiText = fiElem.Body.TextContent.Trim();
                    var fiSplit = FilterBracketText(fiText);

                    fi = fiSplit[0];

                    nb = fiSplit[1];

                    fiLinkDict = new LinkDictionary(fiElem.QuerySelectorAll("a").Where(a => a.HasAttribute("href")));


                    var enElem = Wiktionary.ParseString(m.Groups["en"].Value);
                    en = enElem.Body.TextContent.Trim();
                    enLinks = new LinkDictionary(enElem.QuerySelectorAll("a").Where(a => a.HasAttribute("href")));

                    return;






                }

                // Splits a string into an array of two: that which was outside any parentheses and that which was inside.
                // The string of parentheses is prepended with the index number of where the parenthesis is located in the output string.
                // If there were more than two parenthesis, the two notations are separated by a ':'.
                // e.g. "Lorum (ipsum) sic (dolor) amet." => ["Lorum sic amet.", "[6](ipsum):[10](dolor)"] 
                internal static string[] FilterBracketText(string input, string output = "", string bracket = "", int bracketDepth = 0)
                {
                    if (String.IsNullOrWhiteSpace(input)) return new string[2] { output, bracket };
                    char head = input.First();
                    string tail = input.Substring(1);
                    if (head == ')')
                    {
                        //var remainingString = (!String.IsNullOrEmpty(tail) && tail.First() == ' ') ? tail.Substring(1) : tail; // remove trailing space
                        var tmpParenString = bracket + head;
                        var parenString = (String.IsNullOrEmpty(tail)) ? tmpParenString : tmpParenString + '|';

                        return FilterBracketText(tail, output, parenString, bracketDepth - 1);
                    }
                    if (head == '(')
                    {
                        var newOutput = !String.IsNullOrEmpty(output) && output.Last() == ' ' ? output.Substring(0, output.Length - 1) : output; // remove leading space.
                        return FilterBracketText(tail, newOutput, bracket + '[' + output.Length.ToString() + ']' + head, bracketDepth + 1);
                    }

                    bool isInBracket = bracketDepth > 0;

                    if (isInBracket) return FilterBracketText(tail, output, bracket + head, bracketDepth);
                    else return FilterBracketText(tail, output + head, bracket, 0);
                }

                internal static IEnumerable<Example> GetExamples(IElement dl)
                {
                    var exElems = dl.Children.Where(e => IsExample(e));
                    return exElems.Select(dd => new Example(dd));
                }

                internal static bool IsExample(IElement e)
                {
                    var eHtml = e.OuterHtml.Replace("\n", String.Empty);

                    return (ex1.IsMatch(eHtml) || ex2.IsMatch(eHtml) || ex3.IsMatch(eHtml));

                }

                public void Output()
                {
                    Console.Write("          ○ {0} : {1}", fi, en);
                    if (!String.IsNullOrEmpty(nb)) Console.WriteLine(" NB:{0}", nb);
                    else Console.WriteLine();
                    if (fiLinkDict != null) fiLinkDict.Output();


                }

                public ExampleObject[] GetJsonArray()
                {
                    var enEx = new ExampleObject() { language = "en", links = enLinks.GetJsonObject(), text = en };
                    var fiEx = new ExampleObject() { language = "fi", links = fiLinkDict.GetJsonObject(), text = fi };

                    return new ExampleObject[2] { enEx, fiEx };
                }
            }


        }

        internal class Section : IOutItem
        {
            internal readonly string heading;
            internal readonly IEnumerable<ISectionItem> items;
            internal readonly IEnumerable<Definition> definitions;

            internal Section(IElement head)
            {
                heading = head.FirstElementChild.TextContent.Trim();

                IElement elem = head.NextElementSibling;
                IElement nextElem = elem.NextElementSibling;

                var hasDefinitions = (elem.TagName == "P" && nextElem != null && nextElem.TagName == "OL");

                if (hasDefinitions)
                {
                    definitions = Definition.GetDefinitions(heading, elem.NextElementSibling);
                    items = Enumerable.Empty<ISectionItem>();
                }

                else
                {
                    items = Item.GetItems(elem);
                    definitions = Enumerable.Empty<Definition>();
                }


                //else items = new Item[1] { new Item(elem) }; // assming if it's not a list then it's just a paragraph.
            }

            internal static Section[] GetSections(IElement elem, Section[] sections = null)
            {
                if (sections == null) return GetSections(elem, new Section[0]);
                if (EnFiPageEntry.IsEnd(elem)) return sections;

                if (elem.TagName == "H4")
                {

                    // some sections have no text content
                    //var sectionElems = EnFiPage.GetElementsUntil("H4", elem.NextElementSibling);
                    //bool hasContent = sectionElems.Count() > 0 && sectionElems.Select(e => e.TextContent.Trim()).Aggregate((x, y) => x + y).Length > 0;

                    //if (hasContent) 
                    return GetSections(elem.NextElementSibling, AddSection(sections, new Section(elem)));

                }

                return GetSections(elem.NextElementSibling, sections);


            }

            private static Section[] AddSection(Section[] sections, Section section)
            {
                Section[] newArray = new Section[sections.Length + 1];
                sections.CopyTo(newArray, 0);
                newArray.SetValue(section, sections.Length);
                return newArray;
            }

            public void Output()
            {
                Console.WriteLine("  Section: {0}", heading);
                if (definitions != null) foreach (Definition def in definitions) def.Output();
                if (items != null) foreach (IOutItem item in items) item.Output();
            }

            public IModelObject GetJsonObject()
            {
                var itemObjs = items.Where(i => i.inflections == null || i.inflections.Count() == 0).Select(i => (SectionObject)i.GetJsonObject());
                var defObjs = definitions.Select(d => (DefinitionObject)d.GetJsonObject());

                if (itemObjs.Count() == 1)
                {
                    var linkObjs = items.SelectMany(i => i.links.Select(l => (LinkObject)l.GetJsonObject()));

                    return new SectionObject() { links = linkObjs, definitions = defObjs, body = itemObjs.First().body, header = heading, language = "en" };
                }

                else
                {
                    return new SectionObject() { items = itemObjs, definitions = defObjs, header = heading, language = "en" };

                }


            }
        }

        internal interface ISectionItem : IOutItem
        {
            IEnumerable<Morpheme> inflections { get; }
            IEnumerable<Link> links { get; }
        }

        internal class Item : ISectionItem
        {

            internal readonly string text;
            private readonly LinkDictionary linkDict;


            public IEnumerable<Morpheme> inflections
            {
                get
                {
                    return null;
                }
            }

            public IEnumerable<Link> links
            {
                get
                {
                    return linkDict == null || linkDict.links.Count() == 0 ? Enumerable.Empty<Link>() : linkDict.links;
                }
            }

            internal Item(IElement elem)
            {
                var rawText = elem.TextContent;
                var noNewlinesText = rawText.Replace('\n', ' ');
                var noDoubleSpacesText = Regex.Replace(noNewlinesText, @"\s+", " ");
                var trimmedText = noDoubleSpacesText.Trim();
                text = trimmedText;


                var anchors = elem.QuerySelectorAll("a").Where(a => a.HasAttribute("href"));

                if (anchors.Count() > 0)
                {
                    linkDict = new LinkDictionary(anchors);
                }
                else linkDict = new LinkDictionary();

            }

            public override string ToString()
            {
                return text;
            }


            const string isConjugationMatch = @"<table\s[^<]+inflection-table.+conjugation";
            const string isDeclensionMatch = @"<table\s[^<]+inflection-table.+nominative";
            internal static ISectionItem[] GetItems(IElement elem, ISectionItem[] items = null)
            {
                if (items == null) return GetItems(elem, new Item[0]);
                if (EnFiPage.IsEndOfPage(elem)) return items;

                if (String.IsNullOrEmpty(elem.TextContent.Trim())) return GetItems(elem.NextElementSibling, items);

                switch (elem.TagName)
                {
                    case "H3":
                    case "H4":
                        return items;

                    case "P":
                        var item = new Item(elem);
                        return GetItems(elem.NextElementSibling, items.Concat(new Item[1] { item }).ToArray());

                    case "UL":
                    case "OL":
                    case "DIV":
                    case "DD":
                        var lItems = elem.QuerySelectorAll("li,dl").Select(li => new Item(li)).ToArray();
                        return GetItems(elem.NextElementSibling, items.Concat(lItems).ToArray());
                    case "TABLE":
                        var table = (IHtmlTableElement)elem;

                        var isConjugation = table.Rows.Length == 66;
                        var isDeclension = table.Rows.Length == 22;
                        var isPronounDeclension = table.Rows.Length == 1;


                        //Logger.Note(String.Format("table {0} {1}", table.ClassName, table.Rows.Length));
                        if (isConjugation) return GetItems(elem.NextElementSibling, items.Concat(new ISectionItem[1] { new ConjugationTable(elem) }).ToArray());
                        if (isDeclension) return GetItems(elem.NextElementSibling, items.Concat(new ISectionItem[1] { new DeclensionTable(elem) }).ToArray());
                        if (isPronounDeclension) return GetItems(elem.NextElementSibling, items.Concat(new ISectionItem[1] { new PronounDeclensionTable(elem.QuerySelector("TABLE")) }).ToArray());


                        Logger.Warn(String.Format("unknown table {0}", table.OuterHtml));
                        return GetItems(elem.NextElementSibling, items);
                    case "SCRIPT":
                        return GetItems(elem.NextElementSibling, items);
                    default:
                        return GetItems(elem.NextElementSibling, items.Concat(new ISectionItem[1] { new Item(elem) }).ToArray());
                }
            }

            public void Output()
            {
                Console.Out.WriteLine("    • {0}", text);
                if (linkDict != null) linkDict.Output();

            }

            public IModelObject GetJsonObject()
            {
                return new SectionObject() { body = text, links = linkDict.GetJsonObject() };
            }

            internal class ConjugationTable : WikiTable, ISectionItem, IOutItem
            {
                internal readonly Morpheme[] _inflections;

                public IEnumerable<Morpheme> inflections
                {
                    get
                    {
                        return _inflections;
                    }
                }

                public IEnumerable<Link> links
                {
                    get
                    {
                        return Enumerable.Empty<Link>();
                    }
                }

                internal ConjugationTable(IElement elem) : base(elem)
                {
                    //var i = 0;
                    //foreach (IElement iElem in elem.FirstElementChild.Children)
                    //{
                    //    Console.WriteLine("{0} {1} {2}", i++, iElem.TagName, iElem.TextContent);
                    //}

                    //var maxCol = virtualGrid.Keys.Aggregate((agg, next) => next.Item2 > agg.Item2 ? next : agg).Item2;
                    //var maxRow = virtualGrid.Keys.Aggregate((agg, next) => next.Item1 > agg.Item1 ? next : agg).Item1;

                    //for (int col = 0; col <= maxCol; col++)
                    //{
                    //    for (int row = 0; row <= maxRow; row++)
                    //    {
                    //        var vCell = GetVirtualGridCell(row, col);

                    //        Console.WriteLine("[{0},{1}] {2} {3}", row, col, vCell.elem.TagName, vCell.content);
                    //    }
                    //}

                    var inflectionCells = rows.SelectMany(r => r.cells.Where(c => c.elem.TagName == "TD")).SelectMany(c => c.SplitEntries());
                    _inflections = GetConjugations(inflectionCells);


                }

                private Morpheme[] GetConjugations(IEnumerable<Cell> inflectionCells, Morpheme[] conjugations = null)
                {
                    if (conjugations == null) return GetConjugations(inflectionCells, new Morpheme[0]);

                    if (inflectionCells.Count() == 0) return conjugations;

                    var cell = inflectionCells.First();


                    var tail = inflectionCells.Skip(1);

                    if (String.IsNullOrWhiteSpace(cell.content) || cell.content == "—") return GetConjugations(tail, conjugations);

                    var coords = GetVirtualCellCoordinates(cell);
                    var row = coords.Item1;
                    var col = coords.Item2;



                    var isNominalForms = (row >= 50);

                    if (!isNominalForms)
                    {
                        // sections: rows 4 - 10, 13-19, 23-29, 33-39, 43-49

                        var person = GetVirtualGridCell(row, 0).content;
                        var valence = GetVirtualGridCell(3, col).content;
                        var tenseRow = row <= 10 ? 2 : row <= 19 ? 11 : row <= 29 ? 21 : row <= 39 ? 31 : 41;
                        var tense = GetVirtualGridCell(tenseRow, col).content;
                        var moodRow = row <= 19 ? 1 : row <= 29 ? 20 : row <= 39 ? 30 : 40;
                        var mood = GetVirtualGridCell(moodRow, 0).content;

                        return GetConjugations(tail, conjugations.Concat(new Morpheme[1] { new Morpheme(cell.content, new string[4] { person, valence, tense, mood }) }).ToArray());

                    }
                    else
                    {

                        var isInfinitive = col < 4;

                        // there is one cell that has footnotes that we do not want to include
                        if (row >= 57 && !isInfinitive) return GetConjugations(tail, conjugations);

                        if (isInfinitive)
                        {
                            var infName = GetVirtualGridCell(row, 0).content + " infinitive";
                            var hasCase = row >= 55 && row <= 64;

                            if (hasCase)
                            {
                                var infCase = GetVirtualGridCell(row, 1).content;

                                var hasActivity = row >= 55 && row <= 62;
                                if (hasActivity)
                                {
                                    var infActivity = GetVirtualGridCell(52, col).content;
                                    return GetConjugations(tail, conjugations.Concat(new Morpheme[1] { new Morpheme(cell.content, new string[3] { infName, infCase, infActivity }) }).ToArray());

                                }
                                else return GetConjugations(tail, conjugations.Concat(new Morpheme[1] { new Morpheme(cell.content, new string[2] { infName, infCase }) }).ToArray());


                            }
                            else return GetConjugations(tail, conjugations.Concat(new Morpheme[1] { new Morpheme(cell.content, new string[1] { infName }) }).ToArray());
                        }
                        else // is Participle
                        {
                            var tense = GetVirtualGridCell(row, 4).content;
                            var hasActivity = (row <= 54);
                            var activity = GetVirtualGridCell(52, col).content;

                            var attributes = hasActivity ? new string[3] { activity, tense, "participle" } : new string[2] { tense, "participle" };

                            return GetConjugations(tail, conjugations.Concat(new Morpheme[1] { new Morpheme(cell.content, attributes) }).ToArray());

                        }



                        //return GetConjugations(tail, conjugations);
                    }
                }

                public new void Output()
                {
                    //base.Output();
                    foreach (Morpheme conjugation in _inflections) conjugation.Output();
                }

                public IModelObject GetJsonObject()
                {
                    var inflections = this._inflections.Select(c => new InflectionObject() { attributes = c.attributes, term = c.term });

                    return new InflectionCollection() { inflections = inflections };
                }
            }

            internal class DeclensionTable : WikiTable, ISectionItem
            {
                internal readonly Morpheme[] _inflections;

                public IEnumerable<Morpheme> inflections
                {
                    get
                    {
                        return _inflections;
                    }
                }

                public IEnumerable<Link> links
                {
                    get
                    {
                        return Enumerable.Empty<Link>();
                    }
                }

                internal DeclensionTable(IElement elem) : base(elem)
                {
                    //var maxCol = virtualGrid.Keys.Aggregate((agg, next) => next.Item2 > agg.Item2 ? next : agg).Item2;
                    //var maxRow = virtualGrid.Keys.Aggregate((agg, next) => next.Item1 > agg.Item1 ? next : agg).Item1;

                    //for (int col = 0; col <= maxCol; col++)
                    //{
                    //    for (int row = 0; row <= maxRow; row++)
                    //    {
                    //        var vCell = GetVirtualGridCell(row, col);

                    //        Console.WriteLine("[{0},{1}] {2} {3}", row, col, vCell.elem.TagName, vCell.content);
                    //    }
                    //}

                    var inflectionCells = rows.SelectMany(r => r.cells.SelectMany(c => c.SplitEntries()).Where(c => c.elem.TagName == "TD"));

                    _inflections = GetDeclensions(inflectionCells);
                }

                private Morpheme[] GetDeclensions(IEnumerable<Cell> inflectionCells, Morpheme[] declensions = null)
                {
                    if (declensions == null) return GetDeclensions(inflectionCells, new Morpheme[0]);

                    if (inflectionCells.Count() == 0) return declensions;

                    var cell = inflectionCells.First();
                    var tail = inflectionCells.Skip(1);

                    if (String.IsNullOrWhiteSpace(cell.content) || cell.content == "—") return GetDeclensions(tail, declensions);

                    var coords = GetVirtualCellCoordinates(cell);
                    var row = coords.Item1;
                    var col = coords.Item2;

                    if (row < 5) return GetDeclensions(tail, declensions);

                    var wordCase = GetVirtualGridCell(row, 0).content;
                    var plurality = GetVirtualGridCell(5, col).content;
                    var wordCase2 = (row == 7 || row == 8) && col == 2 ? GetVirtualGridCell(row, 1).content : null;

                    var attributes = (String.IsNullOrEmpty(wordCase2)) ? new string[2] { wordCase, plurality } : new string[3] { wordCase, plurality, wordCase2 };

                    return GetDeclensions(tail, declensions.Concat(new Morpheme[1] { new Morpheme(cell.content, attributes) }).ToArray());

                }

                public new void Output()
                {
                    foreach (Morpheme declension in _inflections) declension.Output();
                }

                public IModelObject GetJsonObject()
                {
                    var inflections = this._inflections.Select(c => new InflectionObject() { attributes = c.attributes, term = c.term });
                    return new InflectionCollection() { inflections = inflections };
                }
            }

            internal class PronounDeclensionTable : WikiTable, ISectionItem
            {
                internal readonly Morpheme[] _inflections;

                public IEnumerable<Morpheme> inflections
                {
                    get
                    {
                        return _inflections;
                    }
                }

                public IEnumerable<Link> links
                {
                    get
                    {
                        return Enumerable.Empty<Link>();
                    }
                }

                internal PronounDeclensionTable(IElement elem) : base(elem)
                {


                    var inflectionCells = rows.SelectMany(r => r.cells.SelectMany(c => c.SplitEntries(',')).SelectMany(c => c.SplitEntries()).Where(c => c.elem.TagName == "TD"));

                    _inflections = GetDeclensions(inflectionCells);
                }

                private Morpheme[] GetDeclensions(IEnumerable<Cell> inflectionCells, Morpheme[] declensions = null)
                {
                    if (declensions == null) return GetDeclensions(inflectionCells, new Morpheme[0]);

                    if (inflectionCells.Count() == 0) return declensions;

                    var cell = inflectionCells.First();
                    var tail = inflectionCells.Skip(1);

                    if (String.IsNullOrWhiteSpace(cell.content) || cell.content == "–") return GetDeclensions(tail, declensions);

                    var coords = GetVirtualCellCoordinates(cell);
                    var row = coords.Item1;
                    var col = coords.Item2;

                    var isHeader = (row == 0 || col == 0 || col == 3);

                    if (isHeader) return GetDeclensions(tail, declensions);

                    var wordCaseCol = col <= 2 ? 0 : 3;

                    var wordCase = GetVirtualGridCell(row, wordCaseCol).content;
                    var plurality = GetVirtualGridCell(0, col).content;

                    var attributes = new string[2] { wordCase, plurality };

                    return GetDeclensions(tail, declensions.Concat(new Morpheme[1] { new Morpheme(cell.content, attributes) }).ToArray());

                }


                public new void Output()
                {
                    foreach (Morpheme declension in _inflections) declension.Output();
                }

                public IModelObject GetJsonObject()
                {
                    var inflections = this._inflections.Select(c => new InflectionObject() { attributes = c.attributes, term = c.term });
                    return new InflectionCollection() { inflections = inflections };
                }
            }
        }


        public void Output()
        {
            Console.Out.WriteLine("\nItem: {0}", term);
            if (entries != null) foreach (EnFiPageEntry entry in entries) entry.Output();
        }

        private SectionObject EntryToSectionObject(EnFiPageEntry entry)
        {
            var heading = entry.heading;
            var jsonObjs = entry.items.Select(i => i.GetJsonObject());
            var itemObjs = (IEnumerable<SectionObject>) jsonObjs.Where(j => (j is SectionObject)).Select(i => (SectionObject) i);

            var inflCols = (IEnumerable<InflectionCollection>) jsonObjs.Where(j => (j is InflectionCollection)).Select(i => (InflectionCollection)i);

            var inflObjs = inflCols.SelectMany(icol => icol.inflections.Select(i => i));

            //var links = entry.items.SelectMany( i => i.l)

            if (itemObjs.Count() == 1)
            {
                return new SectionObject() { body = itemObjs.First().body, header = heading, language = "en", links = itemObjs.First().links, inflections = inflObjs };
            }
            else
            {
                return new SectionObject() { header = heading, language = "en", items = itemObjs, links = itemObjs.SelectMany(i => i.links), inflections = inflObjs };
            }

        }

        public IModelObject GetJsonObject()
        {
            // English Wiktionary pages change layout / CSS hierarchy if a term contains more than one category 
            // (e.g. a term that functions as both a verb and a noun)
            // If it is a term with more than one category, the definitions will not be in the entries themselves but in sub-sections.
            bool isSingleTerm = entries.Where(e => e.definitions != null && e.definitions.Count() > 0).Count() > 0;

            if (isSingleTerm)
            {

                var defEntry = entries.Where(e => e.definitions != null && e.definitions.Count() > 0).First();
                // the entry that has defintions also has category in its heading.

                var catObj = defEntry.heading;
                var defObjs = defEntry.definitions.Select<Definition, DefinitionObject>(d => (DefinitionObject)d.GetJsonObject());

                var inflections = entries.SelectMany(e => e.sections.SelectMany(s => s.items)).Where(i => (i is WikiTable)).SelectMany(t => t.inflections);
                var infObjs = inflections.Select(m => (InflectionObject)m.GetJsonObject());

                var entrySectObjs = entries.Where(e => e != defEntry).Select(e => EntryToSectionObject(e));
                var sectionObjs = entries.SelectMany(e => e.sections.Select(s => (SectionObject)s.GetJsonObject()));

                var combinedSectionObjs = sectionObjs.Concat(entrySectObjs);


                var entry = new EntryObject() { category = defEntry.heading, definitions = defObjs, inflections = infObjs, sections = combinedSectionObjs };



                return new TermObject() { term = term, entries = new EntryObject[1] { entry } };
            }
            else
            {
                // For these purposes, an "EntryObject" is any entry or section with definition
                // and a "SectionObject" is any entry or section without definitions.

                var sections = entries.SelectMany(e => e.sections.Select(s => s));

                // These two collections need to be converted to EntryObjs
                var defSections = sections.Where(s => s.definitions.Count() > 0);
                var defEntries = entries.Where(e => e.definitions != null && e.definitions.Count() > 0);

                // These two collections need to be converted to Sections
                var sectionsEntr = entries.Except(defEntries).Select( e => (SectionObject) EntryModelToSectionObject(e));
                var sectionsSects = sections.Except(defSections).Select( s => (SectionObject) s.GetJsonObject());




                return new TermObject() { term = term, entries=Enumerable.Empty<EntryObject>() };
            }

            // Otherwise, the "entries" do function as entries, and
            // under each there are sections.

            //var entryObjs = entries.Select(e => (EntryObject)e.GetJsonObject());
            //return new TermObject() { term = term, entries = entryObjs };
        }

        
        private SectionObject EntryModelToSectionObject(EnFiPageEntry e)
        {
            return new SectionObject()
            {

            };
        }
    }


}
