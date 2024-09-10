using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    class SearchConditionData
    {
        [DataMember(Name = "type")]
        public string Type { get; private set; }

        [DataMember(Name = "conditions")]
        public List<SearchConditionValue> Conditions { get; private set; } = new();

        public SearchConditionData(string type)
        {
            Type = type;
        }

        public void Validate()
        {
            Conditions.RemoveAll(x => x.IsEmpty());

            if (Conditions.Count == 1)
            {
                AddImplicitRangeCondition(Conditions[0].Range);
            }
        }

        public void AddCondition(SearchConditionValue conditionValue)
        {
            if (conditionValue == null) return;

            var index = Conditions.FindIndex(x => IsConditionOverlapping(x, conditionValue));
            if (index >= 0)
            {
                Conditions[index] = conditionValue;
            }
            else
            {
                Conditions.Add(conditionValue);
            }
        }

        static bool IsConditionOverlapping(SearchConditionValue existingValue, SearchConditionValue newValue)
        {
            if (existingValue.Range == SearchConditionRange.GreaterThanOrEqual || existingValue.Range == SearchConditionRange.GreaterThan)
            {
                return newValue.Range == SearchConditionRange.GreaterThanOrEqual || newValue.Range == SearchConditionRange.GreaterThan;
            }

            if (existingValue.Range == SearchConditionRange.LessThanOrEqual || existingValue.Range == SearchConditionRange.LessThan)
            {
                return newValue.Range == SearchConditionRange.LessThanOrEqual || newValue.Range == SearchConditionRange.LessThan;
            }

            return false;
        }

        void AddImplicitRangeCondition(string conditionRange)
        {
            if (conditionRange == SearchConditionRange.GreaterThanOrEqual || conditionRange == SearchConditionRange.GreaterThan)
            {
                switch (Type)
                {
                    case "date-range":
                        Conditions.Add(new SearchConditionValue(SearchConditionRange.LessThanOrEqual, DateTime.MaxValue));
                        break;
                }
            }
            else if (conditionRange == SearchConditionRange.LessThanOrEqual || conditionRange == SearchConditionRange.LessThan)
            {
                switch (Type)
                {
                    case "date-range":
                        Conditions.Add(new SearchConditionValue(SearchConditionRange.GreaterThanOrEqual, DateTime.MinValue));
                        break;
                }
            }
        }
    }

    [DataContract]
    class SearchConditionValue
    {
        [DataMember(Name = "value")]
        internal string ValueString => ValueToString();

        [DataMember(Name = "conditionType")]
        public string Range { get; private set; }

        object Value { get; }

        public SearchConditionValue(SearchConditionRange conditionRange, object value)
        {
            Range = conditionRange.ToString();
            Value = value;
        }

        internal bool IsEmpty()
        {
            return string.IsNullOrEmpty(Range) || string.IsNullOrEmpty(ValueString);
        }

        string ValueToString()
        {
            if (Value is DateTime dateTime)
            {
                return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }

            return Value?.ToString() ?? string.Empty;
        }
    }
}
