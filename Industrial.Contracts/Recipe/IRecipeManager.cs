using System;
using System.Collections.Generic;

namespace Industrial.Contracts.Recipe
{
    public interface IRecipeManager
    {
        void Add(Recipe recipe);
        void Remove(string recipeName);
        Recipe Get(string recipeName);
        bool TryGet(string recipeName, out Recipe recipe);
        IReadOnlyList<string> GetAllNames();

        void Activate(string recipeName);
        Recipe ActiveRecipe { get; }

        event EventHandler<RecipeActivatedEventArgs> RecipeActivated;
    }

    public sealed class Recipe
    {
        public string Name { get; }
        public string Description { get; }
        public string Version { get; }
        public DateTime CreatedAt { get; }
        private readonly Dictionary<string, RecipeParameter> _params;

        public IReadOnlyDictionary<string, RecipeParameter> Parameters => _params;

        public Recipe(string name, string description = null, string version = "1.0")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Version = version;
            CreatedAt = DateTime.Now;
            _params = new Dictionary<string, RecipeParameter>(StringComparer.OrdinalIgnoreCase);
        }

        public Recipe Set(string key, double value, string unit = null, double? min = null, double? max = null)
        {
            _params[key] = new RecipeParameter(key, value, unit, min, max);
            return this;
        }

        public Recipe Set(string key, string value)
        {
            _params[key] = new RecipeParameter(key, value);
            return this;
        }

        public Recipe Set(string key, bool value)
        {
            _params[key] = new RecipeParameter(key, value);
            return this;
        }

        public double GetDouble(string key) => _params[key].AsDouble();
        public string GetString(string key) => _params[key].AsString();
        public bool GetBool(string key) => _params[key].AsBool();
        public bool Contains(string key) => _params.ContainsKey(key);

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            foreach (var p in _params.Values)
                if (!p.IsValid(out var msg)) errors.Add(msg);
            return errors;
        }
    }

    public sealed class RecipeParameter
    {
        public string Key { get; }
        public object Value { get; }
        public string Unit { get; }
        public double? Min { get; }
        public double? Max { get; }

        public RecipeParameter(string key, object value, string unit = null, double? min = null, double? max = null)
        {
            Key = key;
            Value = value;
            Unit = unit;
            Min = min;
            Max = max;
        }

        public double AsDouble() => Convert.ToDouble(Value);
        public string AsString() => Value?.ToString();
        public bool AsBool() => Convert.ToBoolean(Value);

        public bool IsValid(out string errorMessage)
        {
            if (Value is double d)
            {
                if (Min.HasValue && d < Min.Value)
                { errorMessage = $"{Key}={d} 低于最小值 {Min} {Unit}"; return false; }
                if (Max.HasValue && d > Max.Value)
                { errorMessage = $"{Key}={d} 超过最大值 {Max} {Unit}"; return false; }
            }
            errorMessage = null;
            return true;
        }

        public override string ToString()
            => Unit != null ? $"{Key} = {Value} {Unit}" : $"{Key} = {Value}";
    }

    public sealed class RecipeActivatedEventArgs : EventArgs
    {
        public Recipe Previous { get; }
        public Recipe Current { get; }
        public RecipeActivatedEventArgs(Recipe previous, Recipe current) { Previous = previous; Current = current; }
    }
}
