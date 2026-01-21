using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Globalization;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace TurtleTools
{
    class XmlTools
    {
        private static readonly Dictionary<string, XmlSerializer> Cache = new Dictionary<string, XmlSerializer>();

        private static readonly object SyncRoot = new object();

        public static XmlSerializer Create(Type type, XmlRootAttribute root)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (root == null) throw new ArgumentNullException("root");

            var key = String.Format(CultureInfo.InvariantCulture, "{0}:{1}", type, root.ElementName);

            lock (SyncRoot)
            {
                if (!Cache.ContainsKey(key))
                {
                    Cache.Add(key, new XmlSerializer(type, root));
                }
            }

            return Cache[key];
        }

        public static XmlSerializer Create<T>(XmlRootAttribute root)
        {
            return Create(typeof(T), root);
        }

        public static XmlSerializer Create<T>()
        {
            return Create(typeof(T));
        }

        public static XmlSerializer Create<T>(string defaultNamespace)
        {
            return Create(typeof(T), defaultNamespace);
        }

        public static XmlSerializer Create(Type type)
        {
            return new XmlSerializer(type);
        }

        public static XmlSerializer Create(Type type, string defaultNamespace)
        {
            return new XmlSerializer(type, defaultNamespace);
        }

        static string Serialize<T>(T value, string root = "DocumentElement")
        {
            if (value == null)
                return null;

            //XmlSerializer serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(root));
            XmlSerializer serializer = Create(typeof(T), new XmlRootAttribute(root));

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false, false); // no BOM in a .NET string
            settings.Indent = true;
            settings.OmitXmlDeclaration = false;

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");

            using (ExtentedStringWriter textWriter = new ExtentedStringWriter(Encoding.UTF8))
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(textWriter, settings))
                    serializer.Serialize(xmlWriter, value, ns);

                return textWriter.ToString();
            }
        }

        public static T Deserialize<T>(string xml, string root = "DocumentElement")
        {
            if (string.IsNullOrEmpty(xml))
                return default(T);

            //XmlSerializer serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(root));
            XmlSerializer serializer = Create(typeof(T), new XmlRootAttribute(root));

            XmlReaderSettings settings = new XmlReaderSettings();

            using (StringReader textReader = new StringReader(xml))
                using (XmlReader xmlReader = XmlReader.Create(textReader, settings))
                    return (T)serializer.Deserialize(xmlReader);
        }

        public static void WriteXml<T>(string fpath, T value) 
        {
            FileTools.WriteUTF8XML(fpath, Serialize(value));
        }

        public static T ReadXml<T>(string fpath)
        {
            if (File.Exists(fpath) == false)
                return default(T);

            return Deserialize<T>(File.ReadAllText(fpath, Encoding.UTF8));
        }
    }

    public sealed class ExtentedStringWriter : StringWriter
    {
        private readonly Encoding stringWriterEncoding;
        public ExtentedStringWriter(Encoding desiredEncoding) : base()
        {
            this.stringWriterEncoding = desiredEncoding;
        }

        public override Encoding Encoding
        {
            get
            {
                return this.stringWriterEncoding;
            }
        }
    }
}
