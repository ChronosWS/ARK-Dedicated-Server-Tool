﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ARK_Server_Manager.Lib
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class AggregateIniValueEntryAttribute : Attribute
    {
    }

    /// <summary>
    /// An INI style value of the form AggrevateName=(Key1=val1, Key2=val2...)
    /// </summary>
    public abstract class AggregateIniValue : DependencyObject
    {
        protected readonly List<PropertyInfo> properties = new List<PropertyInfo>();

        public static T FromINIValue<T>(string value) where T : AggregateIniValue, new()
        {
            var result = new T();
            result.InitializeFromINIValue(value);
            return result;
        }

        public T Duplicate<T>() where T : AggregateIniValue, new()
        {
            GetPropertyInfos(true);

            var result = new T();
            foreach (var prop in this.properties)
            {
                if (prop.CanWrite)
                    prop.SetValue(result, prop.GetValue(this));
            }

            return result;
        }

        public virtual string ToINIValue()
        {
            GetPropertyInfos();
            StringBuilder result = new StringBuilder();
            result.Append("(");

            bool firstItem = true;
            foreach (var prop in this.properties)
            {
                if(!firstItem)
                {
                    result.Append(',');
                }

                var val = prop.GetValue(this);
                var propValue = StringUtils.GetPropertyValue(val, prop);

                result.Append(prop.Name).Append('=');
                if (prop.PropertyType == typeof(String))
                {
                    result.Append('"').Append(propValue).Append('"');
                }
                else
                {
                    result.Append(propValue);
                }

                firstItem = false;
            }

            result.Append(")");
            return result.ToString();
        }

        internal static object SortKeySelector(AggregateIniValue arg)
        {
            return arg.GetSortKey();
        }

        public abstract bool IsEquivalent(AggregateIniValue other);
        public abstract string GetSortKey();
        public virtual bool ShouldSave() { return true; }

        protected void GetPropertyInfos(bool allProperties = false)
        {
            if (this.properties.Count == 0)
            {
                if (allProperties)
                    this.properties.AddRange(this.GetType().GetProperties());
                else
                    this.properties.AddRange(this.GetType().GetProperties().Where(p => p.GetCustomAttribute(typeof(AggregateIniValueEntryAttribute)) != null));
            }
        }

        protected virtual void InitializeFromINIValue(string value)
        {
            GetPropertyInfos();

            var kvPair = value.Split(new[] { '=' }, 2);
            value = kvPair[1].Trim('(', ')', ' ');
            var pairs = value.Split(',');

            foreach (var pair in pairs)
            {
                kvPair = pair.Split('=');
                if (kvPair.Length == 2)
                {
                    var key = kvPair[0].Trim();
                    var val = kvPair[1].Trim();
                    var propInfo = this.properties.FirstOrDefault(p => String.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
                    if (propInfo != null)
                    {
                        StringUtils.SetPropertyValue(val, this, propInfo);
                    }
                }
            }
        }
    }
}
