using StronglyTypedIds;

namespace StronglyTypedIds.IntegrationTests.Types
{
    [StronglyTypedId]
    partial struct DefaultId1 { }

    [StronglyTypedId]
    public partial struct DefaultId2 { }

    [StronglyTypedId("Guid")]
    partial struct GuidId1 { }

    [StronglyTypedId("Guid")]
    public partial struct GuidId2 { }
    
    [StronglyTypedId("Int")]
    partial struct IntId { }

    [StronglyTypedId("Long")]
    partial struct LongId { }

    [StronglyTypedId("NewId")]
    partial struct NewIdId1 { }

    [StronglyTypedId("NewId")]
    public partial struct NewIdId2 { }

    [StronglyTypedId("NullableString")]
    partial struct NullableStringId { }

    [StronglyTypedId("String")]
    partial struct StringId { }

    public partial class SomeType<T> where T : new()
    {
        public partial record NestedType<TKey, TValue>
        {
            public partial struct MoreNesting
            {
                [StronglyTypedId]
                public partial struct VeryNestedId {}
            }
        }
    }
}