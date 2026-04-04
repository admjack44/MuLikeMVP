using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// View for monster entities. Handles monster-specific visual states such as aggro.
    /// </summary>
    public class MonsterView : EntityView
    {
        [SerializeField] private GameObject _aggroIndicator;

        private bool _isAggro;

        public void SetAggro(bool aggro)
        {
            _isAggro = aggro;
            if (_aggroIndicator != null)
                _aggroIndicator.SetActive(aggro);

            PlayAnimation(aggro ? "Walk" : "Idle");
        }

        public override void OnDeath()
        {
            base.OnDeath();
            if (_aggroIndicator != null)
                _aggroIndicator.SetActive(false);
        }
    }
}
