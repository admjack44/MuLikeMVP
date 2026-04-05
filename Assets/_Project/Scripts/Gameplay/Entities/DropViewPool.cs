using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    public sealed class DropViewPool : MonoBehaviour
    {
        [SerializeField] private DropView _dropPrefab;
        [SerializeField] private int _initialSize = 20;

        private readonly Queue<DropView> _pool = new();

        private void Awake()
        {
            Warmup();
        }

        public DropView Spawn(Vector3 position)
        {
            if (_pool.Count == 0)
                CreateOne();

            DropView view = _pool.Dequeue();
            view.transform.position = position;
            view.gameObject.SetActive(true);
            return view;
        }

        public void Release(DropView view)
        {
            if (view == null)
                return;

            view.gameObject.SetActive(false);
            _pool.Enqueue(view);
        }

        private void Warmup()
        {
            int count = Mathf.Max(1, _initialSize);
            for (int i = 0; i < count; i++)
                CreateOne();
        }

        private void CreateOne()
        {
            DropView instance;
            if (_dropPrefab == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "DropView_Fallback";
                go.transform.SetParent(transform, false);
                instance = go.AddComponent<DropView>();
            }
            else
            {
                instance = Instantiate(_dropPrefab, transform);
            }

            instance.gameObject.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
}
