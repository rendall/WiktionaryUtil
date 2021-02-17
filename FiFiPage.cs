using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom.Html;
using WiktionaryUtil;

namespace WiktionaryUtil
{

    // This is a class that extracts from a Finnish Wiktionary page a Finnish word.
    // The nomenclature uses language codes to describe the function of the class.
    // The first code refers to the language of the page itself.
    // The second code refers to the language of the word being scraped.

    internal class FiFiPage : WiktionaryPage, IOutItem
    {
        // cf. Lexical Item, Lexeme

        private readonly IEnumerable<IElement> elements;
        private readonly IElement headline;
        internal readonly IEnumerable<FiFiPageEntry> entries;



        //internal readonly WordCategories[] Categories; // lexical category

        // accepts the string representation of a Wikisanakirja (fi.wiktionary.org) page for a Finnish word
        // if the argument is bad, returns a instance where IsFinnish() returns false.
        internal FiFiPage(string page) : base(page)
        {


            headline = QuerySelector("h2:has(span):contains('Suomi')");
            if (headline == null) return;

            elements = GetFinnishElements(headline, new IElement[0]);

            var orphanElements = elements.Skip(1).TakeWhile(elem => elem.TagName != "H3");

            if (orphanElements != null && orphanElements.Count() > 0)
            {
                // TODO: do something aobut this edge case. (q.v. "kuka")
                // Ideally, tack them in order just after the first H3 in elements.
                // for now, just note it
                // Logger.Warn("{0} has {1} orphaned elements", word, orphanElements.Count());
            }

            entries = FiFiPageEntry.GetEntries(elements.ToArray(), new FiFiPageEntry[0]);

        }

        public static FiFiPage GetPage(string term)
        {
            try
            {
                string fPage = Wiktionary.GetTermPage(term, "fi");
                return new FiFiPage(fPage);
            }
            catch (WebException e)
            {
                var statusCode = e.Response == null? 0 : (int)((HttpWebResponse)e.Response).StatusCode;
                if (statusCode != 404) Console.WriteLine(e);
                var errorMessage = (e.Status == WebExceptionStatus.ProtocolError) ? String.Format("{0}:{1}", statusCode, ((HttpWebResponse)e.Response).StatusDescription) : e.Message;
                var s = String.Format(WiktionaryPage.ErrorPage, errorMessage);
                return new FiFiPage(s);
            }
            catch (Exception e)
            {   //TODO: include response info.
                Console.Out.WriteLine(e);

                var s = String.Format(WiktionaryPage.ErrorPage, e.Message);
                return new FiFiPage(s);
            }
        }

        public void Output()
        {
            Console.Out.WriteLine("\n■ {0}", term);
            if (entries != null) foreach (FiFiPageEntry entry in entries) entry.Output();
        }

        public bool IsEmpty()
        {
            return String.IsNullOrEmpty(term);
        }

        public bool IsFinnish()
        {
            return headline != null;
        }

        // returns true if elem is the end of the Finnish word entry of the page
        internal static bool IsEndOfPage(IElement elem)
        {
            if (elem == null) return true;
            if (elem.TagName == "H2" && !elem.TextContent.ToLower().Contains("suomi")) return true;
            if (elem.TagName == "NOSCRIPT") return true;


            return false;
        }

        // returns only those elements that contain information about the Finnish word on this page
        private static IElement[] GetFinnishElements(IElement elem, IElement[] elems)
        {
            if (IsEndOfPage(elem)) return elems;
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

        private static IElement[] GetElementsUntil(string[] tagNames, IElement elem, IElement[] elems = null)
        {
            if (elems == null) return GetElementsUntil(tagNames, elem, new IElement[0]);
            if (IsEndOfPage(elem)) return elems;
            if (tagNames.Contains(elem.TagName)) return elems;

            return GetElementsUntil(tagNames, elem.NextElementSibling, AddElement(elems, elem));
        }

        public IModelObject GetJsonObject()
        {
            return new TermObject() { entries = entries.Select(e => (EntryObject)e.GetJsonObject()), term = this.term };
        }

        internal class FiFiPageEntry : IOutItem
        {
            public readonly string category;
            public readonly Definition[] definitions;
            public readonly string term;
            public readonly string info;
            private readonly LinkDictionary linkDict;
            public static readonly IEnumerable<string> PartsOfSpeech = Enum.GetNames(typeof(FiFiWordCategories)).Select(e => e.ToLower());
            public readonly Section[] sections;
            private readonly DeclensionTable[] declensionTable; // usually only one.
            internal IEnumerable<Morpheme[]> declensions
            {
                get
                {
                    var ty = declensionTable == null ? Enumerable.Empty<Morpheme[]>() : declensionTable.Select(table => table.declensions);
                    return ty;
                }
            }

            internal IEnumerable<Link> links
            {
                get
                {
                    return linkDict == null || linkDict.links.Count() == 0 ? null : linkDict.links;
                }
            }

            private readonly VerbConjugationPage verbConjugation;

            internal IEnumerable<Morpheme[]> conjugations
            {
                get
                {
                    var cons = verbConjugation == null ? Enumerable.Empty<Morpheme[]>() : verbConjugation.conjugations;
                    return cons;
                }
            }

            public FiFiPageEntry(IElement[] elements)
            {
                Invalidate(elements);

                IElement wordElem = GetWordElement(elements.Skip(1));
                term = wordElem.FirstElementChild.TextContent.Trim();

                // entry info order:
                // category
                category = GetWordCategory(elements.First());
                if (!PartsOfSpeech.Contains(category.ToLower()))
                {
                    Console.WriteLine();
                    Logger.Warn(String.Format("{0} has unknown word category {1} ", term, category));
                }


                // word itself + other info

                bool hasVerbConjugation = FiFiPageEntry.HasVerbConjugationLink(wordElem);
                if (hasVerbConjugation) verbConjugation = VerbConjugationPage.GetPage(term);

                IElement infoClone = (IElement)wordElem.Clone();
                infoClone.RemoveChild(infoClone.FirstElementChild);
                info = infoClone.TextContent.Trim();

                var anchors = wordElem.QuerySelectorAll("a").Where(a => a.HasAttribute("href"));

                if (anchors.Count() > 0)
                {
                    linkDict = new LinkDictionary(anchors);
                }
                else linkDict = new LinkDictionary();


                // 'Get' here could be 'Discover'.  DiscoverDefinitions, DiscoverSections, etc.
                // definitions
                definitions = Definition.GetDefinitions(elements.Skip(1).FirstOrDefault(), new Definition[0]);

                // sections
                sections = Section.GetSections(elements.Skip(1).First());

                // inflection tables
                declensionTable = DeclensionTable.GetTables(elements.Skip(1).First()).ToArray();

                //inflections = declensions.SelectMany(iTable => iTable.declensions).ToArray();
            }

            private static bool HasVerbConjugationLink(IElement wordElem)
            {
                if (wordElem == null) return false;
                var link = wordElem.QuerySelectorAll("a").Where(a => a.HasAttribute("href")).Select(a => a.GetAttribute("href")).Where(href => href.StartsWith("/wiki/Liite:Verbitaivutus/suomi/"));
                return link.Count() > 0;
            }

            public void Output()
            {
                Console.Out.WriteLine("\n  ▼ {0} {1} {2}", term, info, category.ToString());
                if (linkDict != null) linkDict.Output();


                foreach (Definition def in definitions) def.Output();

                foreach (Section section in sections) section.Output();

                if (declensionTable != null) foreach (DeclensionTable table in declensionTable) table.Output();

                //if (inflections != null) foreach (Morpheme inf in inflections) inf.Output();

                if (verbConjugation != null) verbConjugation.Output();

            }

            private static IElement GetWordElement(IEnumerable<IElement> elems)
            {
                IElement elem = elems.FirstOrDefault();
                if (IsEnd(elem)) return EmptyWordElement();

                bool isWordElem = ((elem.TagName == "P") && (elem.FirstElementChild.TagName == "B"));

                if (isWordElem) return elem;
                else return GetWordElement(elems.Skip(1));
            }

            private static IElement EmptyWordElement()
            {
                return (IElement)(new HtmlParser()).Parse("<html><head></head><body></body></html>").CreateElement("p");
            }

            // if elem represents a part of speech, returns the part of speech, or null.
            internal static string GetWordCategory(IElement elem)
            {
                if (elem.TagName != "H3")
                {
                    Logger.Note("Improper element tag {0} passed to GetWordCategory", elem.TagName);
                    return null;
                }


                string cat = elem.FirstElementChild.TextContent.Trim();

                return cat;
            }


            // end of section / entry
            internal static bool IsEnd(IElement elem)
            {
                return FiFiPage.IsEndOfPage(elem) || elem.TagName == "H3";
            }

            internal static FiFiPageEntry[] GetEntries(IElement[] elements, FiFiPageEntry[] entries)
            {
                // cycle through Finnish elements on HtmlPage
                IElement elem = elements.FirstOrDefault();
                if (FiFiPage.IsEndOfPage(elem)) return entries;

                // page entries are headed by h3 tags
                // if element is not an h3 then 
                if (elem.TagName != "H3")
                {
                    return GetEntries(elements.Skip(1).ToArray(), entries);
                }


                // otherwise, create a new PageEntry, add it to entries, and continue.
                FiFiPageEntry entry = new FiFiPageEntry(elements);
                return GetEntries(elements.Skip(1).ToArray(), FiFiPageEntry.AddEntry(entries, entry));

            }



            // just throws an error
            private static void Invalidate(IElement[] elements)
            {
                if (elements.Length == 0) throw new ArgumentException("Empty element list");
                if (GetWordCategory(elements.First()) == null) throw new ArgumentException("First element must be a word category entry");

            }

            internal static FiFiPageEntry[] AddEntry(FiFiPageEntry[] pages, FiFiPageEntry page)
            {
                FiFiPageEntry[] newArray = new FiFiPageEntry[pages.Length + 1];
                pages.CopyTo(newArray, 0);
                newArray.SetValue(page, pages.Length);
                return newArray;
            }

            /*
            public string category;
            public IEnumerable<DefinitionObject> definitions;
            public string inflectionType;
            public IEnumerable<InflectionObject> inflections;
            public IEnumerable<SectionObject> sections;
            public IEnumerable<LinkObject> links;
            public SectionObject info;
            */

            public IModelObject GetJsonObject()
            {
                var catObj = category;
                var defObj = definitions.Select(d => (DefinitionObject)d.GetJsonObject());
                var infectionTypeObj = info;
                var declens = declensionTable.SelectMany(t => t.declensions).Select(m => (InflectionObject)m.GetJsonObject());
                var conjs = conjugations.SelectMany(morphArray => morphArray.Select(a => (InflectionObject)a.GetJsonObject()));
                var inflections = declens.Concat(conjs);
                var sectionObjs = sections.Select(s => (SectionObject)s.GetJsonObject());


                return new EntryObject() { category = category, definitions = defObj, inflectionType = info, inflections = inflections, sections = sectionObjs, links = linkDict.GetJsonObject() };
            }
        }

        internal class Definition : IOutItem
        {
            internal readonly string text;
            internal readonly Example[] examples;
            private readonly LinkDictionary linkDict;
            internal readonly int rank;

            internal IEnumerable<Link> links
            {
                get
                {
                    return linkDict == null || linkDict.links.Count() == 0 ? null : linkDict.links;
                }
            }


            internal Definition(IElement e)
            {
                IElement elem = (IElement)e.Clone(true);

                var dl = elem.QuerySelector("dl");


                if (dl != null)
                {
                    examples = Example.GetExamples(dl);
                    elem.RemoveChild(dl);
                }
                else examples = new Example[0];

                var anchors = elem.QuerySelectorAll("a").Where(a => a.HasAttribute("href"));

                if (anchors.Count() > 0)
                {
                    linkDict = new LinkDictionary(anchors);
                    //anchors.ToList().ForEach(a => links.AddLink(a));
                }
                else linkDict = new LinkDictionary();
                text = elem.TextContent.Trim();
            }

            public Definition(IElement e, int rank) : this(e)
            {
                this.rank = rank;
            }

            public void Output()
            {
                Console.Out.WriteLine("\n    ○ {0}", text);
                if (linkDict != null) linkDict.Output();
                if (examples != null) foreach (Example ex in examples) ex.Output();

            }

            public override string ToString()
            {
                return text;
            }

            internal static Definition[] GetDefinitions(IElement elem, Definition[] defs)
            {

                if (FiFiPageEntry.IsEnd(elem)) return defs;


                if (elem.TagName == "OL")
                {
                    var newDefs = GetDefinitions(elem.FirstElementChild, defs);
                    return newDefs;
                }

                if (elem.TagName == "LI")
                {
                    var def = new Definition(elem, defs.Count() + 1);
                    var newDefs = Definition.AddDefinition(defs, def);
                    return GetDefinitions(elem.NextElementSibling, newDefs);
                }

                return GetDefinitions(elem.NextElementSibling, defs);
            }

            private static Definition[] AddDefinition(Definition[] defs, Definition def)
            {
                Definition[] newDefs = new Definition[defs.Length + 1];
                defs.CopyTo(newDefs, 0);
                newDefs[defs.Length] = def;
                return newDefs;
            }

            public IModelObject GetJsonObject()
            {
                var exObjs = examples.Select(e => new ExampleObject[1] { (ExampleObject)e.GetJsonObject() });
                return new DefinitionObject() { text = text, language = "fi", rank = rank, links = linkDict.GetJsonObject(), examples = exObjs };
            }
        }

        internal class Example : IOutItem
        {

            internal readonly string text;
            private readonly LinkDictionary linkDict;
            internal IEnumerable<Link> links
            {
                get
                {
                    return linkDict == null || linkDict.links.Count() == 0 ? null : linkDict.links;
                }
            }


            internal Example(IElement dd)
            {
                text = dd.TextContent.Trim();

                var anchors = dd.QuerySelectorAll("a").Where(a => a.HasAttribute("href"));

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

            internal static Example[] GetExamples(IElement elem)
            {
                return elem.QuerySelectorAll("dd").Select(dd => new Example(dd)).ToArray();
            }

            public void Output()
            {
                Console.Out.WriteLine("        ▸ " + text);
                if (linkDict != null) linkDict.Output();

            }

            public IModelObject GetJsonObject()
            {
                // TODO: implement 'language' here.
                return new ExampleObject() { text = text, links = linkDict.GetJsonObject(), language = "fi" };
            }
        }


        // These always begin with h4 and are usually followed by an unordered list
        //  qv Etymologia, Liittyvät sanat, Aiheesta muualla, Huomautukset
        internal class Section : IOutItem
        {
            internal readonly string heading;
            internal readonly Item[] items;
            private readonly IElement head;

            internal Section(IElement head)
            {
                this.head = head;
                heading = head.FirstElementChild.TextContent.Trim();

                IElement elem = head.NextElementSibling;

                // TODO: Find a better place to put these kinds of content-based rules
                // as this scraper is supposed to be content-agonostic, but I'm adding this
                // here now for debugging purposes, and the "Käännökset" conditional can be removed.
                // just have this line:  

                //items = Item.GetItems(elem);
                items = (heading == "Käännökset") ? Item.GetItems(elem).Where(i => i.text.StartsWith("englanti:")).ToArray() : Item.GetItems(elem);


                //else items = new Item[1] { new Item(elem) }; // assming if it's not a list then it's just a paragraph.
            }

            internal static Section[] GetSections(IElement elem, Section[] sections = null)
            {
                if (sections == null) return GetSections(elem, new Section[0]);
                if (FiFiPageEntry.IsEnd(elem)) return sections;

                var sectionHeadTags = new string[2] { "H4", "H5" };
                if (sectionHeadTags.Contains(elem.TagName))
                {

                    // some sections have no text content
                    var sectionElems = FiFiPage.GetElementsUntil(sectionHeadTags, elem.NextElementSibling);
                    bool hasContent = sectionElems.Count() > 0 && sectionElems.Select(e => e.TextContent.Trim()).Aggregate((x, y) => x + y).Length > 0;

                    if (hasContent) return GetSections(elem.NextElementSibling, AddSection(sections, new Section(elem)));

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

            public IModelObject GetJsonObject()
            {
                var itemObjs = items.Select(i => (SectionObject)i.GetJsonObject());
                return new SectionObject() { language = "fi", items = itemObjs, header = heading };
            }

            public void Output()
            {
                Console.WriteLine("  ► {0}", heading);
                if (items != null) foreach (Item item in items) item.Output();
            }

            internal class Item : IOutItem
            {

                internal readonly string text;
                private readonly LinkDictionary linkDict;
                internal IEnumerable<Link> links
                {
                    get
                    {
                        return linkDict == null || linkDict.links.Count() == 0 ? null : linkDict.links;
                    }
                }


                internal Item(IElement li)
                {
                    text = li.TextContent.Trim();

                    var anchors = li.QuerySelectorAll("a").Where(a => a.HasAttribute("href"));

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

                internal static Item[] GetItems(IElement elem, Item[] items = null)
                {
                    if (items == null) return GetItems(elem, new Item[0]);
                    if (FiFiPage.IsEndOfPage(elem)) return items;

                    if (String.IsNullOrEmpty(elem.TextContent.Trim())) return GetItems(elem.NextElementSibling, items);

                    switch (elem.TagName)
                    {
                        case "H3":
                        case "H4":
                        case "H5":
                            return items;
                        case "UL":
                        case "OL":
                        case "DIV":
                        case "DD":
                            var lItems = elem.QuerySelectorAll("li,dl").Select(li => new Item(li)).ToArray();
                            return GetItems(elem.NextElementSibling, items.Concat(lItems).ToArray());
                        default:
                            return GetItems(elem.NextElementSibling, items.Concat(new Item[1] { new Item(elem) }).ToArray());
                    }
                }

                public void Output()
                {
                    Console.Out.WriteLine("    • {0}", text);
                    if (linkDict != null) linkDict.Output();

                }

                public IModelObject GetJsonObject()
                {
                    return new SectionObject() { language = "fi", body = text, links = linkDict.GetJsonObject() };
                }
            }


        }



        internal class DeclensionTable : WikiTable
        {
            internal readonly Morpheme[] declensions;

            internal DeclensionTable(IElement elem) : base(elem)
            {

                declensions = rows.SelectMany(r => r.cells).Where(c => IsInflectionCell(c)).SelectMany(c => c.SplitEntries()).Select(c => CreateInflection(c)).Where(c => c != null).ToArray();
            }

            // A boolean
            private bool IsInflectionCell(Cell c)
            {


                if (c.column == 0) return false;

                int[] headerRows = { 0, 1, 6, 10, 14 };

                if (headerRows.Contains(c.row)) return false;

                if (c.content.Trim() == "–" || String.IsNullOrWhiteSpace(c.content)) return false;

                return true;
            }

            private Morpheme CreateInflection(Cell c)
            {
                var term = c.content.Replace('\n', ' ');

                var nounCase = GetCell(c.row, 0).content;
                var valence = GetCell(1, c.column).content;

                string[] attributes = { nounCase, valence };

                var links = c.elem.QuerySelectorAll("a").Where(a => a.HasAttribute("href"));

                return new Morpheme(term, attributes, links);
            }

            internal static bool IsDeclensionTable(IElement elem)
            {
                if (!WikiTable.IsWikiTable(elem)) return false;

                var tbody = elem.FirstElementChild;
                var isTBody = tbody.TagName == "TBODY";

                if (!isTBody) return false;

                return tbody.ChildElementCount == 20;
            }

            internal static bool IsEnd(IElement elem)
            {   // this should be moved to WiktionaryPage
                return FiFiPage.IsEndOfPage(elem) || elem.TagName == "H3";
            }

            public new void Output()
            {
                //base.Output();

                if (declensions != null)
                {
                    foreach (Morpheme declension in declensions) declension.Output();
                }

            }

            internal static DeclensionTable[] GetTables(IElement elem, DeclensionTable[] tables = null)
            {
                if (tables == null) return GetTables(elem, new DeclensionTable[0]);
                if (IsEnd(elem)) return tables;

                if (IsDeclensionTable(elem))
                {

                    return GetTables(elem.NextElementSibling, AddTable(tables, new DeclensionTable(elem)));
                }
                else
                {
                    // some sections bury the inflection table in divs, possibly other tags
                    var tableElem = elem.QuerySelector("table");
                    var hasTable = tableElem != null;

                    if (hasTable) return GetTables(tableElem, tables);
                }

                return GetTables(elem.NextElementSibling, tables);
            }

            static DeclensionTable[] AddTable(DeclensionTable[] tables, DeclensionTable table)
            {
                DeclensionTable[] newArray = new DeclensionTable[tables.Length + 1];
                tables.CopyTo(newArray, 0);
                newArray.SetValue(table, tables.Length);
                return newArray;
            }
        }


        // Verbs have an entire page devoted to their conjugation
        // which are here https://fi.wiktionary.org/wiki/Liite:Verbitaivutus/suomi/{verbi}
        internal class VerbConjugationPage : WiktionaryPage
        {
            internal const string VERB_INFLECTION_URI = "https://fi.wiktionary.org/wiki/Liite:Verbitaivutus/suomi/{0}";
            private readonly IEnumerable<ConjugationTable> conjugationTables;
            private readonly IEnumerable<VerbidTable> verbidTables;

            internal IEnumerable<Morpheme[]> conjugations
            {
                get
                {
                    IEnumerable<Morpheme[]> cons = conjugationTables == null ? Enumerable.Empty<Morpheme[]>() : conjugationTables.Select(t => t.conjugations);
                    IEnumerable<Morpheme[]> vers = verbidTables == null ? Enumerable.Empty<Morpheme[]>() : verbidTables.Select(t => t.verbids);

                    return cons.Concat(vers);
                }
            }

            internal VerbConjugationPage(string page) : base(page)
            {
                conjugationTables = QuerySelectorAll("table").Where(table => ConjugationTable.IsConjugationTable(table)).Select(t => new ConjugationTable(t));
                verbidTables = QuerySelectorAll("table").Where(table => VerbidTable.IsVerbidTable(table)).Select(t => new VerbidTable(t));
            }

            public void Output()
            {
                conjugationTables.ToList().ForEach(t => t.Output());
                verbidTables.ToList().ForEach(t => t.Output());
            }

            internal static string GetFinnishVerbInflectionUri(string verb)
            {
                return String.Format(VERB_INFLECTION_URI, verb);
            }

            private static string DownloadVerbPage(string uri)
            {
                return Wiktionary.DownloadPage(uri);
            }

            internal static VerbConjugationPage GetPage(string verb)
            {
                return new VerbConjugationPage(DownloadVerbPage(GetFinnishVerbInflectionUri(verb)));
            }



        }

        internal class ConjugationTable : WikiTable
        {
            internal readonly Morpheme[] conjugations;
            private string header;

            internal ConjugationTable(IElement elem) : base(elem)
            {
                header = elem.QuerySelector("tr").TextContent.Trim();
                // TODO: put a checker here to make sure that 'header' is one of the verbal 'moods' (Indikatiivi, etc)
                // and if not, log a note.
                conjugations = rows.SelectMany(r => r.cells).Where(c => IsInflectionCell(c)).SelectMany(c => c.SplitEntries()).Select(c => CreateInflection(c)).Where(c => c != null).ToArray();
            }

            private Morpheme CreateInflection(Cell c)
            {
                // tapaluokka: Indikatiivi, Konditionaali, mm.

                string[] badContent = { "–", "-\"-" };
                if (String.IsNullOrEmpty(c.content) || badContent.Contains(c.content)) return null;

                var mood = GetCell(0, 0).content.ToLower();

                // aikamuoto: preesens, perfekti, imperfekti, pluskvamperfekti, 
                var tenseCol = c.column < 4 ? 0 : 2;
                var tenseRow = c.row <= 10 ? 1 : 11;
                var tenseCell = GetCell(tenseRow, tenseCol);
                var tense = tenseCell.content;


                // te / Te is a problematic edge case, since these two entries are included in the same row.
                var isTe = c.row == 7 || c.row == 17;


                // fortunately, this cell includes only one entry
                // unfortunately, we have to investigate the content in order to know which entry this is.


                // persoona: minä, sinä, etc.
                var personCell = GetCell(c.row, 0);
                var person = isTe ? c.elem.TextContent.EndsWith(c.content) ? "Te" : "te" : personCell.content;

                var valenceRow = c.row <= 10 ? 2 : 12;

                var valence = GetCell(valenceRow, c.column).content;

                string[] attributes = { mood, tense, person, valence };

                var links =  c.elem.QuerySelectorAll("a").Where(a => a.HasAttribute("href") && !a.GetAttribute("href").Contains("index.php"));

                var contentElem = (IElement)c.elem.Clone();
                var aExternal = contentElem.QuerySelector("a.external");
                if (aExternal != null) contentElem.RemoveChild(aExternal);

                var content = contentElem.TextContent.Trim();

                return new Morpheme(c.content, attributes, links);
            }

            private static bool IsInflectionCell(Cell c)
            {
                int[] headerColumns = { 0, 3, 4 };
                if (headerColumns.Contains(c.column)) return false;
                if (c.elem.TagName == "TH") return false;
                if (String.IsNullOrWhiteSpace(c.content)) return false;

                return true;
            }

            // Edge case: sometimes the content has a 'create' link.
            // we need to remove it, otherwise the inflection word
            // will have a 'luo' suffix.
            // And we need to do it early in the process.
            private static Cell RemoveCreateLink(Cell c)
            {
                // "RemoveChild" works on the IElement instance directly.
                // to avoid that side-effect, we clone the IElement instance,
                // remove the child from that clone, and return a new cell
                // based on the old, using the new cloned element.
                var luoFreeElem = (IElement)c.elem.Clone();
                if (luoFreeElem.QuerySelector("a.external") != null) luoFreeElem.RemoveChild(luoFreeElem.QuerySelector("a.external"));

                return new Cell(luoFreeElem, c.column, c.row);
            }

            internal static bool IsConjugationTable(IElement table)
            {
                if (table.TagName != "TABLE") return false;

                // conjugation tables have a row of 1 cell serving as header,
                // while the other table on the page has a header row with 2 cells.
                bool hasSingleHeader = table.QuerySelector("tr").ChildElementCount == 1;

                return hasSingleHeader;
            }

            public new void Output()
            {
                //base.Output();
                if (conjugations != null)
                {
                    Console.Out.WriteLine("  ► {0}", header);
                    foreach (Morpheme conjugation in conjugations) conjugation.Output();
                }
            }
        }

        internal class VerbidTable : WikiTable
        {
            internal readonly Morpheme[] verbids;

            internal VerbidTable(IElement elem) : base(elem)
            {
                verbids = rows.SelectMany(r => r.cells).Where(c => IsInflectionCell(c)).SelectMany(c => c.SplitEntries()).Select(c => CreateInflection(c)).Where(c => c != null).ToArray();
            }

            private Morpheme CreateInflection(Cell c)
            {

                List<string> attributes = new List<string>();

                var firstCell = GetCell(c.row, 0);

                var isInfinitive = SpanColumn(c) < 4;

                // "rowspan" throws off the column count

                if (isInfinitive)
                {


                    var ordinal = getInfinitiveOrdinal(c.row);

                    attributes.Add(String.Format("{0} infinitiivi", ordinal));

                    // also add second column, if it's there: inessiivi, instruktiivi, etc

                    var hasCase = c.row > 3 && c.row <= 13;

                    if (hasCase)
                    {
                        var caseCell = firstCell.elem.HasAttribute("rowspan") ? GetCell(c.row, 1) : firstCell;
                        attributes.Add(caseCell.content);

                        // some have passive and active forms
                        var hasActPass = (c.row == 4 || c.row == 11);
                        if (hasActPass)
                        {
                            var actPass = c.column == 3 ? "aktiivi" : "passiivi";
                            attributes.Add(actPass);
                        }

                    }





                }

                else
                {
                    if (c.row == 2 || c.row == 3)
                    {
                        var tense = GetCell(c.row, 2).content;
                        var actPass = c.column == 3 ? "aktiivi" : "passiivi";
                        attributes.Add(tense);
                        attributes.Add(actPass);
                    }
                    else
                    {
                        int tenseCol = firstCell.elem.HasAttribute("rowspan") ? 4 : 3;

                        var tense = GetCell(c.row, tenseCol).content;
                        attributes.Add(tense);
                    }

                    attributes.Add("partisiippi");
                }




                return new Morpheme(c.content, attributes.ToArray());
            }

            // 
            private static string getInfinitiveOrdinal(int row)
            {
                if (row == 2) return "1.";
                if (row == 3) return "pitkä 1.";
                if (row <= 5) return "2.";
                if (row <= 11) return "3.";
                if (row <= 13) return "4.";
                return "5.";
            }

            private bool IsInflectionCell(Cell c)
            {

                if (c.elem.TagName == "TH") return false;
                if (c.elem.HasAttribute("rowspan")) return false;

                if (c.content == "–") return false;
                if (String.IsNullOrWhiteSpace(c.content)) return false;

                return true;
            }

            // Edge case: sometimes the content has a 'create' link.
            // we need to remove it, otherwise the inflection word
            // will have a 'luo' suffix.
            // And we need to do it early in the process.
            private Cell RemoveCreateLink(Cell c)
            {
                var luoFreeElem = (IElement)c.elem.Clone();
                if (luoFreeElem.QuerySelector("a.external") != null) luoFreeElem.RemoveChild(luoFreeElem.QuerySelector("a.external"));

                return new Cell(luoFreeElem, c.column, c.row);
            }

            internal static bool IsVerbidTable(IElement table)
            {
                if (table.TagName != "TABLE") return false;

                // verbid tables have a row of 1 cell serving as header,
                // while the other table on the page has a header row with 2 cells.
                bool hasDoubleHeader = table.QuerySelector("tr").ChildElementCount == 2;
                bool has15rows = table.QuerySelectorAll("tr").Count() == 15;

                return hasDoubleHeader && has15rows;
            }

            public new void Output()
            {
                //base.Output();
                if (verbids != null) foreach (Morpheme verbid in verbids) verbid.Output();
            }
        }



        // These are denoted in fi.wiktionary pages (non verbs)
        // by table.wikitable of 3 columns and 20 rows including headers
        // Occasionally something is misspelled, so it's better to rely on
        // the overall form to determine that it is an inflection table.




        /* Finnish : English
        aakkonen : character
        adjektiivi : adjective
        adpositio : adposition
        adverbi : adverb
        artikkeli : article
        erisnimi : proper noun
        interjektio : interjection
        konjunktio : conjunction
        lyhenne : abbreviation
        numeraali : numeral
        postpositio : postposition
        prepositio : preposition
        prefiksi : prefix
        pronomini : pronoun
        substantiivi : noun
        suffiksi : suffix
        supistuma : contraction
        verbi : verb
        */


    }



    enum FiFiWordCategories
    {
        Aakkonen,
        Adjektiivi,
        Adpositio,
        Adverbi,
        Artikkeli,
        Erisnimi,
        Fraasi,
        Interjektio,
        Konjunktio,
        Lyhenne,
        Numeraali,
        Postpositio,
        Prepositio,
        Prefiksi,
        Pronomini,
        Substantiivi,
        //Substantiivisanaliitto,
        Suffiksi,
        Supistuma,
        Verbi
    }

    // These are the recognized types of Sections
    // that represent our best guess of the meaning of the section.
    // Sometimes section headings are mislabeled or misspelled, but
    // this enumeration are all of the known & recognized types of
    // sections, across all wiktionaries.
    enum Roles
    {
        UsageNotes,
        Translations,
        Related,
        Pronunciation,
        Derived,
        Idioms,
        ExternalLinks


    }

    // 'PageEntry' corresponds to a lexical entry, which is to say
    // separate units of meaning that happen to share the same sound representation.
    // For instance, minä is a pronoun, but also a term in psychology that corresponds to 'ego'
    // These each have respective page entries.

 

}

