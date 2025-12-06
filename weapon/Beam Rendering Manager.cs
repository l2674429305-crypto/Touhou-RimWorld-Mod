using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace The_Memories_Of_Phantasm
{
    /// <summary>
    /// This is a MapComponent, and each map will have an instance of it.
    /// It is responsible for managing and drawing all active laser beams on the map.
    /// </summary>
    public class BeamDraw : MapComponent
    {
        private List<Beam> beams = new List<Beam>();

        public BeamDraw(Map map) : base(map) { }

        public void RegisterBeam(Beam beam)
        {
            this.beams.Add(beam);
        }

        // This method is called in the late rendering phase of each frame through a Harmony patch.
        public void DrawBeams()
        {
            if (this.beams.Count == 0) return;

            // Use a reverse loop to safely remove elements while iterating.
            for (int i = this.beams.Count - 1; i >= 0; i--)
            {
                Beam beam = this.beams[i];

                // Check for beam expiration first.
                if (beam.tickCreated + beam.duration < Find.TickManager.TicksGame)
                {
                    this.beams.RemoveAt(i);
                    continue; // Skip to the next beam
                }

                Vector3 start = beam.p1;
                Vector3 end = beam.p2;

                if (!ShouldDraw(start) && !ShouldDraw(end)) continue;

                Vector3 direction = (end - start).normalized;
                float totalLength = (end - start).magnitude;
                float maxRenderLength = 100f;
                float beamLength = Mathf.Min(totalLength, maxRenderLength);

                if (beamLength < 0.01f) continue;

                Vector3 renderEnd = start + direction * beamLength;

                Vector3 displacement = renderEnd - start;

                // Texture direction correction: Align the model's local X-axis (Vector3.right) as the "length" direction.
                Quaternion rotation = Quaternion.FromToRotation(Vector3.right, displacement);

                Vector3 position = (start + renderEnd) / 2f;
                position.y = AltitudeLayer.MoteOverhead.AltitudeFor();

                // The scaling logic must also match: apply length to the X-axis and width to the Z-axis.
                Vector3 scale = new Vector3(beamLength, 1f, beam.width);
                Matrix4x4 matrix = default(Matrix4x4);

                matrix.SetTRS(position, rotation, scale);

                if (beam.material != null)
                {                    
                    Graphics.DrawMesh(MeshPool.plane10, matrix, beam.material, 0);
                }
            }
        }

        private bool ShouldDraw(Vector3 pos)
        {
            return Find.CameraDriver.CurrentViewRect.Contains(pos.ToIntVec3());
        }
    }
}