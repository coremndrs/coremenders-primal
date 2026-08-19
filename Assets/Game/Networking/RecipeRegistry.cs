using System.Collections.Generic;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Singleton registry for all RecipeDef SO assets (TDD §5.7.0, 0.2.10a1) — mirrors
    /// <see cref="DefRegistry"/>. Resolves a recipe by its stable recipeId (ids-not-strings).
    ///
    /// Place this MonoBehaviour in the Action scene (or a DontDestroyOnLoad manager). The
    /// _recipes list is auto-populated from Assets/Game/Data/Recipes by RecipeRegistryEditor,
    /// the same way DefRegistry's list is derived — do not hand-curate it.
    /// </summary>
    public class RecipeRegistry : MonoBehaviour
    {
        public static RecipeRegistry Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [SerializeField] private RecipeDef[] _recipes = System.Array.Empty<RecipeDef>();

        private readonly Dictionary<int, RecipeDef> _byId = new Dictionary<int, RecipeDef>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _byId.Clear();
            foreach (var r in _recipes)
            {
                if (r == null) continue;
                if (_byId.ContainsKey(r.recipeId))
                    Debug.LogWarning($"[RecipeRegistry] Duplicate recipeId {r.recipeId} — '{r.displayName}' ignored (already registered as '{_byId[r.recipeId].displayName}').");
                else
                    _byId[r.recipeId] = r;
            }
            Debug.Log($"[RecipeRegistry] Built with {_byId.Count} recipe(s).");
        }

        /// <summary>Returns the RecipeDef for the given recipeId, or null if not registered.</summary>
        public RecipeDef Get(int recipeId) => _byId.TryGetValue(recipeId, out var r) ? r : null;

        /// <summary>All registered recipes in inspector order.</summary>
        public RecipeDef[] AllRecipes => _recipes;
    }
}
