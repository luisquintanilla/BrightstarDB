namespace BrightstarDB.EntityFramework
{
    /// <summary>
    /// Provides a mapping hint for a .NET property
    /// </summary>
    public class PropertyHint(PropertyMappingType propertyMappingType, string schemaTypeUri = null)
    {
        /// <summary>
        /// Get the type of mapping to apply for the property
        /// </summary>
        public PropertyMappingType MappingType { get; } = propertyMappingType;

        /// <summary>
        /// Get the URI related to the mapping.
        /// </summary>
        /// <remarks>If the mapping type is <see cref="PropertyMappingType.Property"/>, <see cref="PropertyMappingType.Arc"/> or <see cref="PropertyMappingType.InverseArc"/>,
        /// then this property specifies the URI of the RDF property. If the mapping type is <see cref="PropertyMappingType.Id"/> or <see cref="PropertyMappingType.Address"/>,
        /// then this property has its default value (NULL).</remarks>
        public string SchemaTypeUri { get; } = schemaTypeUri;
    }

}
