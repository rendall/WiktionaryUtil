using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WiktionaryUtil
{
    abstract internal class WikiTable
    {
        protected readonly IElement elem;
        protected readonly Row[] rows;
        protected readonly Dictionary<Tuple<int, int>, Cell> virtualGrid;
        //internal readonly IEnumerable<Morpheme> inflections;

        protected WikiTable(IElement elem)
        {
            this.elem = elem;
            rows = elem.QuerySelectorAll("tr").Select((tr, i) => new Row(tr, i)).ToArray();
            virtualGrid = GetVirtualGrid(rows);
        }

        // HTML tables aren't perfect grids given cells' rowspan and colspan attributes,
        // which play havoc with data-crawling.  The header or first cell of the
        // row usually has data that applies to other cells, but a rowspan or
        // colspan places that cell only at the head of one row or column.
        private Dictionary<Tuple<int, int>, Cell> GetVirtualGrid(Row[] rows)
        {
            Dictionary<Tuple<int, int>, Cell> dict = new Dictionary<Tuple<int, int>, Cell>();

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var vCol = 0;

                for (int j = 0; j < row.cells.Length; j++)
                {
                    var cell = row.cells[j];
                    var colSpan = cell.colspan; // should be 1 minimum
                    var rowSpan = cell.rowspan; // should be 1 minimum

                    var cols = new List<int>();

                    while (colSpan > 0)
                    {
                        var potentialCoord = Tuple.Create<int, int>(row.index, vCol);
                        if (dict.Keys.Contains(potentialCoord))
                        {
                            vCol++;
                        }
                        else
                        {
                            dict.Add(potentialCoord, cell);
                            colSpan--;
                            cols.Add(vCol);
                        }
                    }


                    while (rowSpan > 1)
                    {
                        var vRow = row.index + (rowSpan - 1);
                        foreach (int col in cols)
                        {
                            var newCoord = Tuple.Create<int, int>(vRow, col);
                            dict.Add(newCoord, cell);
                        }

                        rowSpan--;
                    }

                }
            }

            return dict;
        }

        protected Tuple<int, int> GetVirtualCellCoordinates(Cell cell)
        {
            // because of 'split entries', requested cell may not be the cell in the virtualgrid,
            // but it will have identical coordinates.
            // so we retrive the actual cell if it's not in the values collection.
            var vCell = virtualGrid.Values.Contains(cell) ? cell : virtualGrid.Values.Where(c => c.column == cell.column && c.row == cell.row).FirstOrDefault();

            var coords = virtualGrid.Keys.Where(coord => virtualGrid[coord] == vCell);
            var lowestRow = coords.Aggregate((agg, next) => next.Item1 < agg.Item1 ? next : agg).Item1;
            var lowestCoord = coords.Where(c => c.Item1 == lowestRow).Aggregate((agg, next) => next.Item2 < agg.Item2 ? next : agg);

            return lowestCoord;
        }

        protected Cell GetCell(int row, int col)
        {
            if (row >= rows.Length || col >= rows[row].cells.Length)
            {
                Logger.Error("Out of bounds error in GetCell: {0} ", elem.OuterHtml);
                return rows[0].cells[0];
            }
            return rows[row].cells[col];
        }


        // This function returns a the cell as if the table were a grid of
        // cells with 1 column each. This will probably not 
        // correspond to GetCell().
        protected Cell GetVirtualGridCell(int row, int col)
        {
            return virtualGrid[Tuple.Create(row, col)];
        }

        internal static bool IsWikiTable(IElement elem)
        {
            var isTable = elem.TagName == "TABLE";

            if (!isTable) return false;

            var isWikiTable = elem.ClassList.Contains("wikitable");

            if (!isWikiTable) return false;

            return true;
        }

        protected class Row
        {
            internal readonly Cell[] cells;
            internal readonly int index;

            internal Row(IElement tr, int row)
            {
                index = row;
                cells = tr.QuerySelectorAll("th,td").Select((cell, col) => new Cell(cell, col, row)).ToArray();
            }

            public void Output()
            {
                Console.Out.WriteLine("row {0}", index);
                if (cells != null) foreach (Cell cell in cells)
                    {
                        Console.Out.Write("spancol:{0} ", WikiTable.SpanColumn(cell, this));
                        cell.Output();
                    }
            }
        }

        protected class Cell
        {
            internal readonly int colspan;
            internal readonly int rowspan;

            internal readonly int column;
            internal readonly string content;
            internal readonly int row;
            internal readonly IElement elem;

            private static string GetTextContent(IElement c)
            {
                var cElem = (IElement)c.Clone();
                var sup = cElem.QuerySelector("sup");
                if (sup != null) cElem.RemoveChild(sup);

                return cElem.TextContent.Trim();

            }

            internal Cell(IElement c, int col, int row)
            {
                content = GetTextContent(c);
                var nothing = 0; // boolean int.TryParse insists on an out var.
                colspan = c.HasAttribute("colspan") && int.TryParse(c.GetAttribute("colspan"), out nothing) ? int.Parse(c.GetAttribute("colspan")) : 1;
                rowspan = c.HasAttribute("rowspan") && int.TryParse(c.GetAttribute("rowspan"), out nothing) ? int.Parse(c.GetAttribute("rowspan")) : 1;
                column = col;
                this.row = row;
                elem = c;
            }

            internal Cell(Cell cell, string textContent)
            {
                content = textContent;
                colspan = cell.colspan;
                column = cell.column;
                row = cell.row;
                elem = cell.elem;
            }

            // some cell text content has several entries
            // this returns an array of cells that have the
            // same traits, except for one entry each
            internal Cell[] SplitEntries(char delimiter = '\n')
            {
                if (isSplitEntriesException(this)) return new Cell[1] { this };
                var entries = content.Split(delimiter).Select(e => e.Trim());
                return entries.Select(e => new Cell(this, e)).ToArray();
            }

            private bool isSplitEntriesException(Cell cell)
            {
                //Console.WriteLine("{0} {1} {2}", cell.content, cell.column, cell.row);
                var isKomitatiiviMonikko = (cell.row == 19 && cell.column == 2 && cell.content.Contains("-\n+"));

                if (isKomitatiiviMonikko) return true;

                return false;
            }

            public void Output()
            {
                Console.Out.WriteLine("\tcell{0} {1} colspan:{2}", column, content, colspan);
            }
        }

        // returns the 0 index column - including colspans - of Cell c
        // that is, what column cell c would be if colspans were included as that number of columns
        protected int SpanColumn(Cell c)
        {
            if (c.column == 0) return 0;
            var leftCell = GetCell(c.row, c.column - 1);

            return leftCell.colspan + SpanColumn(leftCell);
        }

        protected static int SpanColumn(Cell c, Row r)
        {
            if (c.column == 0) return 0;
            var leftCell = r.cells[c.column - 1];

            return leftCell.colspan + SpanColumn(leftCell, r);
        }

        // HTML tables aren't perfect grids with rowspan and colspan attributes,
        // which plays havoc with data-crawling.  The header or first cell of the
        // row usuall has data that applies to other cells, but a rowspan or
        // colspan places that cell only at the head of one row or column.
        // This function returns a gridded table where cells with rowspan and colspan 
        // are sliced into an equivalent number of cells with identical content.
        // This cannot be pure FP.   


        protected void Output()
        {
            Console.Out.WriteLine("\n  table");
            var maxCol = virtualGrid.Keys.Aggregate((agg, next) => next.Item2 > agg.Item2 ? next : agg).Item2;
            var maxRow = virtualGrid.Keys.Aggregate((agg, next) => next.Item1 > agg.Item1 ? next : agg).Item1;

            for (int col = 0; col <= maxCol; col++)
            {
                for (int row = 0; row <= maxRow; row++)
                {
                    var vCell = GetVirtualGridCell(row, col);

                    Console.WriteLine("[{0},{1}] {2} {3}", row, col, vCell.elem.TagName, vCell.content);
                }
            }

        }
    }


    // A morpheme is an abstraction of the concept of 
    // inflection, declension, conjugation: it is a word
    // that is different from but related to the base word. 
    // e.g. 'dogs' is a morpheme of the word 'dog'.

    // A morpheme instance is a word tagged with a number of attributes.
    // e.g. minä : nominitiivi, yksikkö
    // e.g. koiriksi : translatiivi, monikko
    internal class Morpheme : IOutItem
    {
        internal readonly string term;
        internal readonly string[] attributes;
        private readonly LinkDictionary linkDict;
        internal IEnumerable<Link> links
        {
            get
            {
                return linkDict == null || linkDict.links.Count() == 0 ? null : linkDict.links;
            }
        }

        internal Morpheme(string term)
        {
            this.term = term;
            attributes = new string[0];
        }

        internal Morpheme(string word, string[] attributes) : this(word)
        {
            this.attributes = attributes;
        }

        internal Morpheme(string word, string[] attributes, IHtmlCollection<IElement> links) : this(word, attributes)
        {
            this.linkDict = new LinkDictionary(links);
        }

        internal Morpheme(string word, string[] attributes, IEnumerable<IElement> links) : this(word, attributes)
        {
            this.linkDict = new LinkDictionary(links);
        }

        internal Morpheme[] AddMorpheme(Morpheme[] morphemes, Morpheme m)
        {
            Morpheme[] newArray = new Morpheme[morphemes.Length + 1];
            morphemes.CopyTo(newArray, 0);
            newArray.SetValue(m, morphemes.Length);
            return newArray;
        }

        public void Output()
        {
            Console.Out.Write("    | {0} | ", term);
            Console.Out.WriteLine("{0}", String.Join(", ", attributes));
            if (linkDict != null) linkDict.Output();
        }

        public IModelObject GetJsonObject()
        {
            return new InflectionObject() { term = term, attributes = attributes };
        }
    }

    internal class LinkDictionary
    {
        internal readonly IEnumerable<Link> links;

        internal LinkDictionary(IHtmlCollection<IElement> anchors)
        {
            links = anchors.Select(a => LinkFromElement(a)).ToList();
        }

        internal LinkDictionary(IEnumerable<Link> links)
        {
            this.links = links;
        }

        internal LinkDictionary(IEnumerable<IElement> anchors)
        {
            links = anchors.Select(a => LinkFromElement(a)).ToList();
        }

        internal LinkDictionary()
        {
            links = Enumerable.Empty<Link>();
        }

        private static Link LinkFromStrings(string content, string href, string title)
        {
            return new Link(content, href, title);
        }

        private static Link LinkFromElement(IElement a)
        {

            var text = a.TextContent.Trim();
            var titleNull = a.GetAttribute("title");
            var title = titleNull == null ? "" : text;

            return LinkFromStrings(text, a.GetAttribute("href"), title);
        }



        public void Output()
        {
            var goodlinks = links.Where(a => a.href != null).Where(a => !a.href.Contains("action=edit&redlink=1"));
            foreach (Link link in goodlinks) link.Output();
        }

        internal IEnumerable<LinkObject> GetJsonObject()
        {
            return links.Select(l => (LinkObject)l.GetJsonObject());
        }
    }

    internal class Link : IOutItem
    {
        internal readonly string title;
        internal readonly string href;
        internal readonly string content;
        internal Link(string content, string href)
        {
            this.content = content;
            this.href = href;
        }

        internal Link(string content, string href, string title) : this(content, href)
        {
            if (href == null) throw new Exception("null href attempted");
            this.title = title;
        }

        public void Output()
        {
            if (content == title) Console.Out.WriteLine("\t\t□ {0} : {1}", content, href);
            else Console.Out.WriteLine("\t\t□ {0} : {1} ({2})", title, href, content);
        }


        public IModelObject GetJsonObject()
        {
            return new LinkObject() { title = title, href = href, text = content };
        }
    }

}
