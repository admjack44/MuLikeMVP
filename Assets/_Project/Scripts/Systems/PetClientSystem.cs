using UnityEngine;

namespace MuLike.Systems
{
    /// <summary>
    /// Manages the local player's active pet state, received from the server.
    /// </summary>
    public class PetClientSystem
    {
        public struct PetState
        {
            public int PetId;
            public string Name;
            public int Level;
            public int HpCurrent;
            public int HpMax;
        }

        public PetState? ActivePet { get; private set; }

        public event System.Action<PetState?> OnPetChanged;

        public void SetActivePet(PetState state)
        {
            ActivePet = state;
            OnPetChanged?.Invoke(ActivePet);
        }

        public void ClearPet()
        {
            ActivePet = null;
            OnPetChanged?.Invoke(null);
        }

        public void UpdateHp(int current, int max)
        {
            if (!ActivePet.HasValue) return;

            var state = ActivePet.Value;
            state.HpCurrent = current;
            state.HpMax = max;
            ActivePet = state;
            OnPetChanged?.Invoke(ActivePet);
        }
    }
}
