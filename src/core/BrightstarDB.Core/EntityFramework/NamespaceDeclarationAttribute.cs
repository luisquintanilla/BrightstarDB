using System;

namespace BrightstarDB.EntityFramework
{
    /// <summary>
    /// Assembly attribute that specifies a prefix mapping for a namespace
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class NamespaceDeclarationAttribute(string prefix, string reference) : Attribute
    {
        /// <summary>
        /// The prefix used to reference the namespace
        /// </summary>
        public string Prefix { get; } = prefix;
        /// <summary>
        /// The base namespace URI that the prefix references
        /// </summary>
        public string Reference { get; } = reference;
    }
}