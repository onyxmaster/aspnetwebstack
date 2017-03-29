// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace System.Web.WebPages
{
    public class AttributeValue
    {
        public AttributeValue(string prefix, object value, bool literal)
        {
            Prefix = prefix;
            Value = value;
            Literal = literal;
        }

        public string Prefix { get; private set; }
        public object Value { get; private set; }
        public bool Literal { get; private set; }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "We are using tuples here to avoid dependencies from Razor to WebPages")]
        public static AttributeValue FromTuple(Tuple<string, object, bool> value)
        {
            return new AttributeValue(value.Item1, value.Item2, value.Item3);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "We are using tuples here to avoid dependencies from Razor to WebPages")]
        public static AttributeValue FromTuple(Tuple<string, string, bool> value)
        {
            return new AttributeValue(value.Item1, value.Item2, value.Item3);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "We are using tuples here to avoid dependencies from Razor to WebPages")]
        public static implicit operator AttributeValue(Tuple<string, object, bool> value)
        {
            return FromTuple(value);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "We are using tuples here to avoid dependencies from Razor to WebPages")]
        public static implicit operator AttributeValue(Tuple<string, string, bool> value)
        {
            return FromTuple(value);
        }
    }
}
