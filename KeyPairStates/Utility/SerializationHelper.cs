using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
namespace KeyPairStates.Utility
{
    public static class SerializationHelper<T>
    {
        /// <summary>
        /// Deserialize the xml element
        /// </summary>
        /// <param name="xmlString">a string variable</param>
        /// <param name="xmlData">an object</param>
        /// <returns>return serializer as an object</returns>
        public static T Deserialize(string xmlString, T xmlData)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(xmlData.GetType());

                // Create a string reader
                using (StringReader stringReader = new StringReader(xmlString))
                {
                    // Use the Deserialize method to restore the object's state.
                    return (T)serializer.Deserialize(stringReader);
                }
            }
            catch
            {
                return default(T);
            }
        }

        public static T Deserialize(string xmlString)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (StringReader stringReader = new StringReader(xmlString))
                {
                    return (T)serializer.Deserialize(stringReader);
                }
            }
            catch
            {
                return default(T);
            }
        }
        public static bool CanDeserialize(string xmlString)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                XmlReader reader = XmlReader.Create(new StringReader(xmlString));
                return serializer.CanDeserialize(reader);
            }
            catch
            {
                return false;
            }

        }
        public static string Serialize(object o, string nsPrefix, string defaultNamespace)
        {
            try
            {
                bool omitXmlDeclaration = true;

                XmlSerializer serializer = new XmlSerializer(typeof(T), defaultNamespace);
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add(nsPrefix, defaultNamespace);

                using (StringWriter stringWriter = new StringWriter())
                {
                    XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
                    xmlWriterSettings.OmitXmlDeclaration = omitXmlDeclaration;
                    xmlWriterSettings.Indent = false;

                    using (XmlWriter xmlWriter = System.Xml.XmlWriter.Create(stringWriter, xmlWriterSettings))
                    {
                        serializer.Serialize(xmlWriter, o, ns);
                    }

                    return stringWriter.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string Serialize(object o)
        {
            return Serialize(o, true);
        }

        public static string Serialize(object o, bool omitXmlDeclaration)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (StringWriter stringWriter = new StringWriter())
                {
                    XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
                    xmlWriterSettings.OmitXmlDeclaration = omitXmlDeclaration;
                    xmlWriterSettings.Indent = false;

                    using (XmlWriter xmlWriter = System.Xml.XmlWriter.Create(stringWriter, xmlWriterSettings))
                    {
                        serializer.Serialize(xmlWriter, o);
                    }

                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return string.Empty;
            }
        }

        /// <summary>
        /// Serialize the xml element and replace the root node
        /// </summary>
        /// <param name="index">an int variable</param>
        /// <param name="childNode">an Object</param>
        /// <param name="renameNode">a string variable</param>
        /// <returns>return xmlDoc as a string</returns>
        public static string Serialize(T[] childNode, string renameNode)
        {
            XmlDocument xmlDoc = new XmlDocument();
            StringBuilder stringBuilder = new StringBuilder();

            // Omitting the XML version
            XmlWriterSettings xmlSettings = new XmlWriterSettings();
            xmlSettings.Indent = false;
            xmlSettings.OmitXmlDeclaration = true;

            using (XmlWriter writer = XmlWriter.Create(stringBuilder, xmlSettings))
            {
                if (childNode != null)
                {
                    // If the parent node is not null then append child nodes
                    XmlSerializer serializer = new XmlSerializer(childNode.GetType());
                    serializer.Serialize(writer, childNode);
                    xmlDoc.LoadXml(stringBuilder.ToString());
                    XmlElement newXmlDocElement = xmlDoc.CreateElement(renameNode);
                    while (xmlDoc.DocumentElement.HasChildNodes)
                    {
                        newXmlDocElement.AppendChild(xmlDoc.DocumentElement.ChildNodes[0]);
                    }
                    xmlDoc.RemoveChild(xmlDoc.DocumentElement);
                    xmlDoc.AppendChild(newXmlDocElement);
                }
                else
                {
                    //if the parent node is null then replace a elment as root
                    XmlElement xmlNodeElement = xmlDoc.CreateElement(renameNode);
                    xmlDoc.AppendChild(xmlNodeElement);
                }
            }
            return xmlDoc.InnerXml;
        }
    }
}
