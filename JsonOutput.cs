using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WiktionaryUtil
{
    public class TermObject : IModelObject
    {
        public IEnumerable<EntryObject> entries;
        public string term;
    }

    public class EntryObject : IModelObject
    {
        public string category;
        public string inflectionType;
        public SectionObject info;
        public IEnumerable<DefinitionObject> definitions;
        public IEnumerable<InflectionObject> inflections;
        public IEnumerable<SectionObject> sections;
        public IEnumerable<LinkObject> links;
    }

    public class DefinitionObject : IModelObject
    {
        public int rank;
        public string text;
        public string language;
        public IEnumerable<ExampleObject[]> examples;
        public IEnumerable<LinkObject> links;

    }

    public class InflectionCollection : IModelObject
    {
        public IEnumerable<InflectionObject> inflections;
    }

    public class InflectionObject : IModelObject
    {
        public string term;
        public string[] attributes;
    }

    public class SectionObject : IModelObject
    {
        public string header;
        public string body;
        public string language;
        public IEnumerable<DefinitionObject> definitions;
        public IEnumerable<InflectionObject> inflections;
        public IEnumerable<SectionObject> items;
        public IEnumerable<LinkObject> links;
    }

    public class LinkObject : IModelObject
    {
        public string title;
        public string text;
        public string href;
    }

    public class ExampleObject : IModelObject
    {
        public string language;
        public string text;
        public IEnumerable<LinkObject> links;
    }

    public class ExampleCollection : IModelObject
    { 
        public IEnumerable<ExampleObject[]> examples;
    }

    public interface IModelObject
    {

    }
}
