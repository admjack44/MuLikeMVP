using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Classes
{
    /// <summary>
    /// Central lookup point for class/evolution definitions.
    /// Required by design: all class/evolution queries should go through this registry.
    /// </summary>
    public sealed class MuClassRegistry : MonoBehaviour
    {
        [SerializeField] private MuClassDefinition[] _definitions = Array.Empty<MuClassDefinition>();

        private readonly Dictionary<MuClassId, MuClassDefinition> _byId = new();

        public IReadOnlyDictionary<MuClassId, MuClassDefinition> Definitions => _byId;

        private void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            _byId.Clear();
            if (_definitions == null)
                return;

            for (int i = 0; i < _definitions.Length; i++)
            {
                MuClassDefinition def = _definitions[i];
                if (def == null || def.classId == MuClassId.Unknown)
                    continue;

                _byId[def.classId] = def;
            }
        }

        public bool TryGetClass(MuClassId classId, out MuClassDefinition definition)
        {
            if (_byId.Count == 0)
                Rebuild();

            return _byId.TryGetValue(classId, out definition);
        }

        public bool TryGetEvolution(MuClassId classId, MuEvolutionTier tier, out MuClassEvolutionData evolution)
        {
            evolution = default;
            if (!TryGetClass(classId, out MuClassDefinition definition) || definition.evolutions == null)
                return false;

            for (int i = 0; i < definition.evolutions.Count; i++)
            {
                MuClassEvolutionData candidate = definition.evolutions[i];
                if (candidate.tier != tier)
                    continue;

                evolution = candidate;
                return true;
            }

            return false;
        }

        public IReadOnlyList<MuClassEvolutionData> GetEvolutions(MuClassId classId)
        {
            return TryGetClass(classId, out MuClassDefinition definition)
                ? (IReadOnlyList<MuClassEvolutionData>)definition.evolutions
                : Array.Empty<MuClassEvolutionData>();
        }
    }
}
