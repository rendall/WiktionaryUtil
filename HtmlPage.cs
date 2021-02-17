using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom.Events;
using System.IO;
using System.Net;

namespace WiktionaryUtil
{

    // wraps the html parser and query library and outputs primitives
    // or core classes
    internal class WiktionaryPage
    {
        protected static readonly string EmptyPage = "<html><head></head><body></body></html>";
        protected static readonly string ErrorPage = Wiktionary.ErrorPage;
        private static HtmlParser _parser;
        protected IHtmlDocument _doc;
        protected readonly string _page;
        public readonly string term;

        public WiktionaryPage(string page)
        {
            if (_parser==null) _parser = new HtmlParser();
            _page = page;
            _doc = _parser.Parse(page);

            var h1 = QuerySelector("h1");

            term = h1 == null ? "" : h1.TextContent;
        }

        public string ErrorMessage()
        {
            var error = QuerySelector("p.error");

            if (error == null) return "";
            return error.TextContent;
        }

        public IElement QuerySelector(string selectors)
        {
            return _doc.QuerySelector(selectors);
        }

        public IHtmlCollection<IElement> QuerySelectorAll(string selectors)
        {
            return _doc.QuerySelectorAll(selectors);
        }      
    }
}
