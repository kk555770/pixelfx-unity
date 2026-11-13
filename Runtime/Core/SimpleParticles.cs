using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>
    /// A simple simulated particle list, for effects that keep state. A port of the Particles class in pixelfx's
    /// src/core/particles.js; pixelfx's index.js exports sim.js's Particles under that name instead, so no library effect uses it.
    /// </summary>
    public sealed class SimpleParticles
    {
        public sealed class Particle
        {
            public double X, Y, Vx, Vy, Life, Age, G, Drag;
            public double? Ground, Bounce;
            /// <summary>Anything the effect wants to draw it with.</summary>
            public object Data;
        }

        public readonly List<Particle> List = new List<Particle>();

        public Particle Add(Particle p)
        {
            p.Age = 0;
            List.Add(p);
            return p;
        }

        public void Update(double dt)
        {
            for (int i = List.Count - 1; i >= 0; i--)
            {
                Particle p = List[i];
                if ((p.Age += dt) >= p.Life)
                {
                    List.RemoveAt(i);
                    continue;
                }
                p.Vx -= p.Vx * p.Drag * dt;
                p.Vy += p.G * dt - p.Vy * p.Drag * dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                if (p.Ground is double ground && p.Y > ground)
                {
                    p.Y = ground;
                    p.Vy = -System.Math.Abs(p.Vy) * (p.Bounce ?? 0.3);
                    p.Vx *= 0.7;
                }
            }
        }

        /// <summary>Life left, 1 at birth to 0 at death.</summary>
        public static double K(Particle p) => Clamp(1 - p.Age / p.Life);
    }
}
