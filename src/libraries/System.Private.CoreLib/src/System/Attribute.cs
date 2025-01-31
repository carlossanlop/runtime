// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    [Serializable]
    [TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public abstract partial class Attribute
    {
        protected Attribute() { }

#if !NATIVEAOT
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:UnrecognizedReflectionPattern",
            Justification = "Unused fields don't make a difference for equality")]
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            Type thisType = this.GetType();
            object thisObj = this;
            object? thisResult, thatResult;

            while (thisType != typeof(Attribute))
            {
                FieldInfo[] thisFields = thisType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (int i = 0; i < thisFields.Length; i++)
                {
                    thisResult = thisFields[i].GetValue(thisObj);
                    thatResult = thisFields[i].GetValue(obj);

                    if (!AreFieldValuesEqual(thisResult, thatResult))
                    {
                        return false;
                    }
                }
                thisType = thisType.BaseType!;
            }

            return true;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:UnrecognizedReflectionPattern",
            Justification = "Unused fields don't make a difference for hashcode quality")]
        public override int GetHashCode()
        {
            Type type = GetType();

            while (type != typeof(Attribute))
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                object? vThis = null;

                for (int i = 0; i < fields.Length; i++)
                {
                    object? fieldValue = fields[i].GetValue(this);

                    // The hashcode of an array ignores the contents of the array, so it can produce
                    // different hashcodes for arrays with the same contents.
                    // Since we do deep comparisons of arrays in Equals(), this means Equals and GetHashCode will
                    // be inconsistent for arrays. Therefore, we ignore hashes of arrays.
                    if (fieldValue != null && !fieldValue.GetType().IsArray)
                        vThis = fieldValue;

                    if (vThis != null)
                        break;
                }

                if (vThis != null)
                    return vThis.GetHashCode();

                type = type.BaseType!;
            }

            return type.GetHashCode();
        }
#endif

        // Compares values of custom-attribute fields.
        private static bool AreFieldValuesEqual(object? thisValue, object? thatValue)
        {
            if (thisValue == null && thatValue == null)
                return true;
            if (thisValue == null || thatValue == null)
                return false;

            Type thisValueType = thisValue.GetType();

            if (thisValueType.IsArray)
            {
                // Ensure both are arrays of the same type.
                if (!thisValueType.Equals(thatValue.GetType()))
                {
                    return false;
                }

                Array thisValueArray = (Array)thisValue;
                Array thatValueArray = (Array)thatValue;
                if (thisValueArray.Length != thatValueArray.Length)
                {
                    return false;
                }

                // Attributes can only contain single-dimension arrays, so we don't need to worry about
                // multidimensional arrays.
                Debug.Assert(thisValueArray.Rank == 1 && thatValueArray.Rank == 1);
                for (int j = 0; j < thisValueArray.Length; j++)
                {
                    if (!AreFieldValuesEqual(thisValueArray.GetValue(j), thatValueArray.GetValue(j)))
                    {
                        return false;
                    }
                }
            }
            else
            {
                // An object of type Attribute will cause a stack overflow, but is unpractical to fight every recursion here.
                // There are many ways the default implementation of Equals for ValueTypes or Attributes can lead to an infinite recursion. It is not practical to prevent it.
                // If users will hit this, they should declare custom Equals.
                if (!thisValue.Equals(thatValue))
                    return false;
            }

            return true;
        }

        public virtual object TypeId => GetType();

        public virtual bool Match(object? obj) => Equals(obj);

        public virtual bool IsDefaultAttribute() => false;
    }
}
