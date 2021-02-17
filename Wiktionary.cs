using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using AngleSharp.Parser.Html;
using AngleSharp.Dom.Html;
using AngleSharp.Dom;

namespace WiktionaryUtil
{
    public class Wiktionary
    {
        const string DEFAULT_LANGUAGE_CODE = "en";

        const string WIKTIONARY_URI = "https://{0}.wiktionary.org/wiki/{1}";

        internal static readonly HtmlParser parser = new HtmlParser();

        internal static string ErrorPage = "<html><head></head><body><p class=\"error\">{0}</p></body></html>";

        static WebClient GetWebClient()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            WebClient webClient = new WebClient
            {
                Encoding = System.Text.Encoding.UTF8
            };
            return webClient;
        }


        public static TermObject GetTerm(string term)
        {
            FiFiPage fifiPage = FiFiPage.GetPage(term);
            var fifiObject = (!fifiPage.IsEmpty() && fifiPage.IsFinnish()) ? (TermObject)fifiPage.GetJsonObject() : new TermObject() { term = term, entries = Enumerable.Empty<EntryObject>() };

            EnFiPage enfiPage = EnFiPage.GetPage(term);
            var enfiObject = (!enfiPage.IsEmpty() && enfiPage.IsFinnish()) ? (TermObject)enfiPage.GetJsonObject() : new TermObject() { term = term, entries = Enumerable.Empty<EntryObject>() };

            var combinedTerms = new TermObject()
            {
                term = fifiObject.term,
                entries = fifiObject.entries.Concat(enfiObject.entries)
            };

            return combinedTerms;

        }

        /*
         * returns the Wiktionary uri of word "word".
         * languageCode is the language the page is in, not the language of the word itself.
         * As of now, the language of the word is always Finnish ("Suomi" or "fi").
         */
        public static string GetTermUri(string word, string languageCode = DEFAULT_LANGUAGE_CODE)
        {
            return String.Format(WIKTIONARY_URI, languageCode, word);
        }



        internal static string DownloadPage(string uri)
        {
                return GetWebClient().DownloadString(uri);
        }

        internal static string GetTermPage(string word, string language = Wiktionary.DEFAULT_LANGUAGE_CODE)
        {
            return DownloadPage(GetTermUri(word, language));
        }

        /*
         * returns true if 'page' contains an entry for the page's word in language represented by 'languageCode'
         * throws an error if 'languageCode' is not supported or otherwise unknown.
         * returns false if 'languageCode' is supported, but 'page' does not contain an entry in the target language.
         * As of now, the target language is always Finnish ("Suomi" or "fi").
         */
        internal static bool PageHasLanguage(string page, string languageCode)
        {
            switch (languageCode)
            {
                case "fi":
                    return page.Contains("<span class=\"mw-headline\" id=\"Finnish\">Finnish</span>") ||
                        page.Contains("<span class=\"mw-headline\" id=\"Suomi\">Suomi</span>");
                default:
                    throw new Exception(String.Format("Language code '{0}' is unsupported or unknown.", languageCode));
            }
        }

        internal static bool IsFinnish(string page)
        {
            return PageHasLanguage(page, "fi");
        }

        internal static IDocument ParseString(string html)
        {
            return parser.Parse(html);
        }

        internal static IDocument GetDocument(string uri)
        {
            string html = DownloadPage(uri);
            return ParseString(html);
        }

        /*  Strip text of all undesirable characters: 
         *  \r 
         *  \n and 
         *  more than one space in a row. 
         */
        internal static string NormalizeText(string text, string normalized = "")
        {
            if (String.IsNullOrEmpty(text)) return normalized;
            char head = text.First();
            string tail = text.Substring(1);

            switch (head)
            {
                case '\r':
                    return NormalizeText(tail, normalized);
                case '\n':
                    if (!String.IsNullOrEmpty(normalized) && normalized.Last() == ' ') return NormalizeText(tail, normalized);
                    else return NormalizeText(tail, normalized + ' ');
                case ' ':
                    if (!String.IsNullOrEmpty(normalized) && normalized.Last() == ' ') return NormalizeText(tail, normalized);
                    else return NormalizeText(tail, normalized + head);
                default:
                    return NormalizeText(tail, normalized + head);
                    
            }
        }
    }
}
