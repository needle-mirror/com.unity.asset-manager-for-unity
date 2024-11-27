using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface IMetadata
    {
        public string Name { get; }

        public object GetValue();
    }

    [Serializable]
    class TextMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_Value;

        public string Name => m_Name;
        public string Value => m_Value;

        public TextMetadata(string name, string value)
        {
            m_Name = name;
            m_Value = value;
        }

        public object GetValue() => Value;

        public override string ToString()
        {
            return Value;
        }
    }

    [Serializable]
    class BooleanMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        bool m_Value;

        public string Name => m_Name;
        public bool Value => m_Value;

        public BooleanMetadata(string name, bool value)
        {
            m_Name = name;
            m_Value = value;
        }

        public string GetName() => Name;

        public object GetValue() => Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    [Serializable]
    class NumberMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        double m_Value;

        public string Name => m_Name;
        public double Value => m_Value;

        public NumberMetadata(string name, double value)
        {
            m_Name = name;
            m_Value = value;
        }

        public string GetName() => Name;

        public object GetValue() => Value;

        public override string ToString()
        {
            var valueAsString = Value.ToString("G");

            if (string.IsNullOrWhiteSpace(valueAsString))
            {
                valueAsString = "0";
            }

            if (valueAsString.StartsWith('.'))
            {
                valueAsString = valueAsString.Insert(0, "0");
            }
            else if (valueAsString.EndsWith('.'))
            {
                valueAsString = valueAsString.Insert(valueAsString.Length, "0");
            }

            if (double.TryParse(valueAsString, out var parsedNumber))
            {
                return parsedNumber.ToString();
            }

            return "Invalid number";
        }
    }

    [Serializable]
    class UrlMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        Uri m_Value;

        public string Name => m_Name;
        public Uri Value => m_Value;

        public UrlMetadata(string name, Uri value)
        {
            m_Name = name;
            m_Value = value;
        }

        public string GetName() => Name;

        public object GetValue() => Value;

        public override string ToString()
        {
            if (Uri.TryCreate(Value.ToString(), UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }

            return "Invalid URL";
        }
    }

    [Serializable]
    class TimestampMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        DateTime m_Value;

        public string Name => m_Name;
        public DateTime Value => m_Value;

        public TimestampMetadata(string name, DateTime value)
        {
            m_Name = name;
            m_Value = value;
        }

        public string GetName() => Name;

        public object GetValue() => Value;

        public override string ToString()
        {
            return Utilities.DatetimeToString(Value);
        }
    }

    [Serializable]
    class UserMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_Value;

        public string Name => m_Name;
        public string Value => m_Value;

        public UserMetadata(string name, string value)
        {
            m_Name = name;
            m_Value = value;
        }

        public string GetName() => Name;

        public object GetValue() => Value;

        public override string ToString()
        {
            return Value;
        }
    }

    [Serializable]
    class SingleSelectionMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_Value;

        public string Name => m_Name;
        public string Value => m_Value;

        public SingleSelectionMetadata(string name, string value)
        {
            m_Name = name;
            m_Value = value;
        }

        public string GetName() => Name;

        public object GetValue() => Value;

        public override string ToString()
        {
            return Value;
        }
    }

    [Serializable]
    class MultiSelectionMetadata : IMetadata
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        List<string> m_Value;

        public string Name => m_Name;
        public  List<string> Value => m_Value;

        public string GetName() => Name;

        public MultiSelectionMetadata(string name, List<string> value)
        {
            m_Name = name;
            m_Value = value;
        }

        public object GetValue() => Value;

        public override string ToString()
        {
            return string.Join(", ", Value);
        }
    }
}
