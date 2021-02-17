using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WiktionaryUtil
{
    public class ListBuilder
    {


        private static readonly string EN_ABSOLUTE_URL = "http://en.wiktionary.org{0}";

        // meta-pages are separated into Index and Category types.
        // Unfortunately, while most entries are in common, there are also entries they do not share.
        // It's necessary to scrape each type.
        private static readonly string EN_INDEX_ENTRY_URI = "https://en.wiktionary.org/wiki/Index:Finnish";
        private static readonly string EN_INDEX_LINK_SELECTOR = "a[title*='Index:Finnish']"; // these are pages to load
        private static readonly string EN_INDEX_ENTRY_SELECTOR = "#mw-content-text > ul > li > a"; // these are entries
        private static readonly string EN_CATEGORY_LINK_SELECTOR = "a[href^=\"/wiki/Category:Finnish\"],a[href*=\"/wiki/Category:fi:\"],a[title^=\"Category:Finnish\"],a[title^=\"Category:fi:\"]";
        private static readonly string EN_CATEGORY_ENTRY_SELECTOR = "#mw-content-text > ul > li > a, .mw-category-group > ul > li > a, a[href*=\"#Finnish\"]"; // these are entries

        private static readonly string[] CategoryUrls = new string[] { "https://en.wiktionary.org/wiki/Category:Finnish_language" };
 
        public static IEnumerable<string> GetEn()
        {
            var categoryEntries = GetCategoryEntries(ImmutableList.Create<string>(CategoryUrls));
            var indexEntries = GetIndexEntries(ImmutableList.Create<string>(EN_INDEX_ENTRY_URI));

            return indexEntries.AddRange(categoryEntries).Distinct();

            //return categoryEntries;
            //return indexEntries;

        }
 
        private static ImmutableList<string> LinksFromIndexPage(string url)
        {
            //Console.Write(url);
            try
            {
                IDocument doc = Wiktionary.GetDocument(url);
                var links = doc.QuerySelectorAll(EN_INDEX_LINK_SELECTOR).Select(a => a.GetAttribute("href")).Select(href => String.Format(EN_ABSOLUTE_URL, href)).ToImmutableList<string>();
                //Console.WriteLine();
                return links;
            }
            catch (Exception)
            {
                //Console.WriteLine(" - error.");
                return ImmutableList.Create<string>();
            }

        }

        private static ImmutableList<string> GetIndexEntries(ImmutableList<string> urlsToFollow, ImmutableList<string> noFollow = null, ImmutableList<string> entries = null)
        {
            if (noFollow == null) return GetIndexEntries(urlsToFollow, ImmutableList.Create<string>());
            if (urlsToFollow.IsEmpty) return entries;
            var url = urlsToFollow.First();
            var remainingUrls = urlsToFollow.Skip(1).ToImmutableList();
            var noFollowMore = noFollow.Add(url);
            var links = LinksFromIndexPage(url).Except(noFollowMore);

            if (entries == null) return GetIndexEntries(urlsToFollow, noFollow, ImmutableList.Create<string>());
            var pageEntries = EntriesFromIndex(url).Except(entries);
            var totalEntries = entries.AddRange(pageEntries);

            

            Console.Write("{0} new:{1} total:{2}", url, pageEntries.Count(), totalEntries.Count());

            if (pageEntries.Where(e => e.Contains(":")).Count() > 1) Console.WriteLine(" *");
            else Console.WriteLine();

            return GetIndexEntries(remainingUrls.AddRange(links).Distinct().ToImmutableList(), noFollowMore, totalEntries);

        }

        private static ImmutableList<string> EntriesFromIndex(string url)
        {
            try
            {
                IDocument doc = Wiktionary.GetDocument(url);
                var entries = doc.QuerySelectorAll(EN_INDEX_ENTRY_SELECTOR).Where(a => !a.GetAttribute("href").Contains("redlink=1")).Select(a => a.TextContent).ToImmutableList<string>();
                return entries;
            }
            catch (Exception)
            {

                return ImmutableList.Create<string>();
            }

        }

        private static ImmutableList<string> EntriesFromCategory(string url)
        {
            try
            {
                IDocument doc = Wiktionary.GetDocument(url);
                var entries = doc.QuerySelectorAll(EN_CATEGORY_ENTRY_SELECTOR)
                    .Where(a => !a.GetAttribute("href").Contains("redlink=1")) // eliminates links without entries
                    .Where(a => !a.TextContent.Contains(":")) // eliminates Appendix: and Template: entries.
                    .Select(a => a.TextContent).ToImmutableList<string>();
                //entries.ForEach(e => Console.WriteLine(e));
                return entries;
            }
            catch (Exception)
            {

                return ImmutableList.Create<string>();
            }

        }

        private static ImmutableList<string> LinksFromCategoryPage(string url)
        {
            try
            {
                //Console.WriteLine(url);
                IDocument doc = Wiktionary.GetDocument(url);
                var links = doc.QuerySelectorAll(EN_CATEGORY_LINK_SELECTOR).Select(a => a.GetAttribute("href"))
                    .Where(href => href.StartsWith("/wiki/") || href.Contains("en.wiktionary.org"))
                    .Select(href => href.StartsWith("//") ? "http:" + href : href)
                    .Select(href => href.Contains("en.wiktionary.org") ? href : String.Format(EN_ABSOLUTE_URL, href))
                    .ToImmutableList<string>();
                return links;
            }
            catch (Exception)
            {

                return ImmutableList.Create<string>();
            }

        }

        private static ImmutableList<string> GetCategoryEntries(ImmutableList<string> urlsToFollow, ImmutableList<string> noFollow = null, ImmutableList<string> entries = null)
        {
            if (noFollow == null) return GetCategoryEntries(urlsToFollow, ImmutableList.Create<string>());
            if (urlsToFollow.IsEmpty) return entries;
            var url = urlsToFollow.First();
            var remainingUrls = urlsToFollow.Skip(1).ToImmutableList();
            var noFollowMore = noFollow.Add(url);
            var links = LinksFromCategoryPage(url).Except(noFollowMore);

            if (entries == null) return GetCategoryEntries(urlsToFollow, noFollow, ImmutableList.Create<string>());

            var pageEntries = EntriesFromCategory(url);
            var newEntries = pageEntries.Except(entries);
            var totalEntries = entries.AddRange(pageEntries);

            Console.WriteLine("{0} \tpage entries:{1} new:{2} total:{3} new pages:{4} remaining pages:{5}", url, pageEntries.Count(), newEntries.Count(), totalEntries.Count(), links.Count(), remainingUrls.Count());

            //if (pageEntries.Where(e => e.Contains(":")).Select(e => { Console.WriteLine(e); return e; }).Count() > 1)
            //{
            //    throw new Exception(url + " contains a bad entry");
            //}
            //else Console.WriteLine();

            return GetCategoryEntries(remainingUrls.AddRange(links).Distinct().ToImmutableList(), noFollowMore, totalEntries);

        }

        //private static IImmutableList<string> GetEntries(ImmutableList<string> urlsToFollow, ImmutableList<string> entries = null)
        //{
        //    if (entries == null) return GetEntries(urlsToFollow, ImmutableList.Create<string>());
        //    if (urlsToFollow.IsEmpty) return entries;
        //    var url = urlsToFollow.First();
        //    var remainingUrls = urlsToFollow.Skip(1).ToImmutableList();
        //    var pageEntries = EntriesFromPage(url).Except(entries);
        //    var newEntries = entries.AddRange(pageEntries);
        //    return GetEntries(remainingUrls, newEntries);
        //}

    }
}
