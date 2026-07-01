using Industrial.Contracts.Logging;
using Industrial.Contracts.Recipe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

// 避免 namespace Industrial.Recipe 与类型 Recipe 同名冲突 (CS0118)
using RecipeModel = Industrial.Contracts.Recipe.Recipe;

namespace Industrial.Recipe
{
    /// <summary>
    /// 配方管理器：加载、存储、切换配方。
    /// </summary>
    public sealed class RecipeManager : IRecipeManager
    {
        private readonly ConcurrentDictionary<string, RecipeModel> _recipes =
            new ConcurrentDictionary<string, RecipeModel>(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger _logger;
        private RecipeModel _activeRecipe;

        public event EventHandler<RecipeActivatedEventArgs> RecipeActivated;

        public RecipeModel ActiveRecipe => _activeRecipe;

        public RecipeManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(typeof(RecipeManager));
        }

        public void Add(RecipeModel recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            var errors = recipe.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    $"配方 [{recipe.Name}] 参数验证失败:\n" + string.Join("\n", errors));

            _recipes[recipe.Name] = recipe;
            _logger.Info($"配方已加载: [{recipe.Name}] v{recipe.Version}");
        }

        public void Remove(string recipeName)
        {
            if (_activeRecipe?.Name == recipeName)
                throw new InvalidOperationException($"配方 [{recipeName}] 当前处于激活状态，无法删除");

            _recipes.TryRemove(recipeName, out _);
        }

        public RecipeModel Get(string recipeName)
        {
            if (_recipes.TryGetValue(recipeName, out var r)) return r;
            throw new KeyNotFoundException($"未找到配方: [{recipeName}]");
        }

        public bool TryGet(string recipeName, out RecipeModel recipe)
            => _recipes.TryGetValue(recipeName, out recipe);

        public IReadOnlyList<string> GetAllNames()
            => _recipes.Keys.OrderBy(k => k).ToList().AsReadOnly();

        public void Activate(string recipeName)
        {
            var recipe = Get(recipeName);
            var previous = _activeRecipe;
            _activeRecipe = recipe;

            _logger.Info($"配方切换: [{previous?.Name ?? "无"}] → [{recipe.Name}]");
            RecipeActivated?.Invoke(this, new RecipeActivatedEventArgs(previous, recipe));
        }
    }
}
