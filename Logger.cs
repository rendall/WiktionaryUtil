using System;
using System.Net;
using AngleSharp.Dom;

namespace WiktionaryUtil
{
    public class Logger
    {
        public static void Note(string message)
        {
            Out("Note", message);
        }

        public static void Warn(string message)
        {
            Out("Warn", message);
        }

        private static void Out(string priority, string message)
        {
            Console.Out.WriteLine("{0}: {1}", priority, message);
        }

        private static void Out(string priority, string format, params object[] arg)
        {
            string outmessage = priority + ": " + format;
            Console.Out.WriteLine(outmessage, arg);
        }

        public static void Note(string message, object obj)
        {
            Note(String.Format(message, obj));
        }

        public static void Error(string message, object obj, object obj2)
        {
            Out("Error", String.Format(message, obj, obj2));
        }

        internal static void Note(string message, params object[] arg)
        {
            Out("Note", message, arg);
        }

        internal static void Warn(string v, object obj)
        {
            Out("Warn", v, obj);
        }

        public static void Warn(string v, object obj1, object obj2)
        {
            Out("Warn", String.Format(v, obj1, obj2));
        }

        public static void Note(string v, object obj1, object obj2)
        {
            Out("Note", String.Format(v, obj1, obj2));
        }

        internal static void Error(string v, string obj)
        {
            Out("Error", String.Format(v, obj));
        }
    }
}