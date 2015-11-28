using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public class AggregateIniValueList<T> : SortableObservableCollection<T>, IIniValuesCollection
         where T : AggregateIniValue, new()
    {
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

        public void AddRange(IEnumerable<T> values)
        {
            foreach (var value in values)
            {
                base.Add(value);
            }
        }

        public string IniCollectionKey { get; }

        private Func<IEnumerable<T>> resetFunc;

        public AggregateIniValueList(string aggregateValueName, Func<IEnumerable<T>> resetFunc)
        {
            this.IniCollectionKey = aggregateValueName;
            this.resetFunc = resetFunc;
        }


        public void Reset()
        {
            this.Clear();
            this.AddRange(this.resetFunc());
        }

        public IEnumerable<string> ToIniValues()
        {
            var values = new List<string>();
            values.AddRange(this.Where(d => d.ShouldSave())
                                .Select(d => String.Format("{0}={1}", this.IniCollectionKey, d.ToINIValue())));
            return values;
        }

        public void FromIniValues(IEnumerable<string> iniValues)
        {
            this.Clear();
            this.AddRange(iniValues.Select(v => AggregateIniValue.FromINIValue<T>(v)));
            this.IsEnabled = (this.Count != 0);

            // Add any default values which were missing
            var defaultItemsToAdd = this.resetFunc().Where(r => !this.Any(v => v.IsEquivalent(r))).ToArray();
            this.AddRange(defaultItemsToAdd);

            this.Sort(AggregateIniValue.SortKeySelector);
        }
    }
}
