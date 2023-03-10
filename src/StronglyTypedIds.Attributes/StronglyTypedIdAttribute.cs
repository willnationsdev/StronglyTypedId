using System;

namespace StronglyTypedIds
{
    /// <summary>
    /// Place on partial structs to make the type a strongly-typed ID
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("STRONGLY_TYPED_ID_USAGES")]
    public sealed class StronglyTypedIdAttribute : Attribute
    {
        /// <summary>
        /// Make the struct a strongly typed ID.
        /// </summary>
        /// <param name="templateName">The name of the template to use to generate the ID.
        /// Templates must be added to the project using the format StronglyTypedId_NAME.txt,
        /// where NAME is the name of the template passed in <paramref name="templateName"/>.
        /// </param>
        public StronglyTypedIdAttribute(string templateName)
        {
            TemplateName = templateName;
        }

        /// <summary>
        /// Make the struct a strongly typed ID, using the default settings
        /// </summary>
        public StronglyTypedIdAttribute()
        {
            TemplateName = null;
        }

        /// <summary>
        /// The <see cref="Type"/> to use to store the strongly-typed ID value.
        /// </summary>
        public string? TemplateName { get; }
    }
}