using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.ContentPipeline.Authoring
{
    [Serializable]
    public struct SpawnPointRecord
    {
        public int monsterId;
        public float x;
        public float y;
        public float z;
        public int count;
    }

    [Serializable]
    public struct SpawnTableRecord
    {
        public string tableId;
        public int mapId;
        public SpawnPointRecord[] entries;
    }

    [CreateAssetMenu(fileName = "SpawnTableDatabase", menuName = "MuLike/Content Pipeline/Spawn Table Database")]
    public sealed class SpawnTableDatabase : ScriptableObject
    {
        public List<SpawnTableRecord> tables = new();
    }

    [Serializable]
    public struct DropEntryRecord
    {
        public int itemId;
        public int chancePercent;
        public int minQuantity;
        public int maxQuantity;
    }

    [Serializable]
    public struct DropTableRecord
    {
        public string tableId;
        public DropEntryRecord[] entries;
    }

    [CreateAssetMenu(fileName = "DropTableDatabase", menuName = "MuLike/Content Pipeline/Drop Table Database")]
    public sealed class DropTableDatabase : ScriptableObject
    {
        public List<DropTableRecord> tables = new();
    }
}
