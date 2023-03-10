using System;

namespace StronglyTypedIds
{
    /// <summary>
    /// Used to control the default Place on partial structs to make the type a strongly-typed ID
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("STRONGLY_TYPED_ID_USAGES")]
    public sealed class StronglyTypedIdDefaultsAttribute : Attribute
    {
        /// <summary>
        /// The <see cref="Type"/> to use to store the strongly-typed ID value
        /// </summary>
        /// <summary>
        /// Set the default values used for strongly typed ids
        /// </summary>
        /// <param name="templateName">The name of the template to use to generate the ID.
        /// Templates must be added to the project using the format StronglyTypedId_NAME.txt,
        /// where NAME is the name of the template passed in <paramref name="templateName"/>.
        /// </param>
        public StronglyTypedIdDefaultsAttribute(string templateName)
        {
            TemplateName = templateName;
        }

        /// <summary>
        /// The <see cref="Type"/> to use to store the strongly-typed ID value
        /// </summary>
        public string TemplateName { get; }
    }
}