using UnityEngine;
using Verse;

namespace The_Memories_Of_Phantasm
{
    /// <summary>
    /// 一个轻量级的纯数据类，用于在内存中存储一道激光束的信息。
    /// 它不继承任何RimWorld的类，因此开销极小。
    /// </summary>
    public class Beam
    {
        public Vector3 p1;
        public Vector3 p2;
        public float width;
        public int duration;
        public int tickCreated;
        public Material material;

        public Beam(Vector3 p1, Vector3 p2, float width, int duration, Material material)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.width = width;
            this.duration = duration;
            this.tickCreated = Find.TickManager.TicksGame;
            this.material = material;
        }
    }
}