using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public abstract class IniValueList<T> : SortableObservableCollection<T>, IIniValuesCollection
    {
        public Func<T, string> ToIniValue { get; }
        public Func<string, T> FromIniValue { get; }
        private Func<IEnumerable<T>> ResetFunc { get; }
        private Func<T, T, bool> EquivalencyFunc { get; }
        private Func<T, object> SortKeySelectorFunc { get; set; }

        public IniValueList(
            string aggregateValueName, 
            Func<IEnumerable<T>> resetFunc,
            Func<T, T, bool> equivalencyFunc,
            Func<T, object> sortKeySelectorFunc,
            Func<T, string> toIniValue, 
            Func<string, T> fromIniValue)
        {
            this.ToIniValue = toIniValue;
            this.FromIniValue = fromIniValue;
            this.ResetFunc = resetFunc;
            this.EquivalencyFunc = equivalencyFunc;
            this.SortKeySelectorFunc = sortKeySelectorFunc;
            this.IniCollectionKey = aggregateValueName;
        }

        private bool isEnabled;
        public bool IsEnabled
        {
            get { return this.isEnabled; }
            set
            {
                this.isEnabled = value;
                OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public string IniCollectionKey { get; }

        public void Reset()
        {
            this.Clear();
            this.AddRange(this.ResetFunc());
        }

        public void FromIniValues(IEnumerable<string> values)
        {
            this.Clear();
            this.AddRange(values.Select(v => v.Substring(v.IndexOf('=') + 1)).Select(this.FromIniValue));
            this.IsEnabled = (this.Count != 0);

            // Add any default values which were missing
            var defaultItemsToAdd = this.ResetFunc().Where(r => !this.Any(v => this.EquivalencyFunc(v, r))).ToArray();
            this.AddRange(defaultItemsToAdd);

            this.Sort(this.SortKeySelectorFunc);
        }

        public IEnumerable<string> ToIniValues()
        {
            var values = new List<string>();
            values.AddRange(this.Select(d => String.Format("{0}={1}", this.IniCollectionKey, this.ToIniValue(d))));
            return values;
        }

        public void AddRange(IEnumerable<T> values)
        {
            foreach (var value in values)
            {
                base.Add(value);
            }
        }
    }
}
