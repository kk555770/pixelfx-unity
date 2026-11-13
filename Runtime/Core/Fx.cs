using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>
    /// What an effect does over time. Override Draw(p, t) and DrawBack(p, t): t is the effect's own clock as the JS original
    /// passes it (wrapped for a loop, held at the end of a one-shot). Draw(p) and DrawBack(p) are for effects that keep their own clock.
    /// </summary>
    public abstract class Effect
    {
        public virtual void Update(double dt) { }
        public virtual void DrawBack(Painter p) { }
        public virtual void DrawBack(Painter p, double t) => DrawBack(p);
        public virtual void Draw(Painter p) { }
        public virtual void Draw(Painter p, double t) => Draw(p);
    }

    public sealed class Emitter
    {
        public int X, Y;

        public void Shift(Painter p, int bornX, int bornY) => p.Shift(bornX - X, bornY - Y);
    }

    public sealed class Module
    {
        public readonly string Name;
        public readonly string Source;
        public readonly int W, H, OX, OY;
        public readonly double Duration;
        public readonly bool Loop;
        public readonly int Fps;
        public readonly Options Defaults;
        /// <summary>The performance: poses, hits, dust and targets (null when the effect has none).</summary>
        public readonly Act Act;
        /// <summary>meta.actor: 0 none, 1 or 2 the stock actor at that scale, handed to the effect as options["actor"].</summary>
        public readonly int Actor;
        /// <summary>How visible the stand-in character is at time t (the JS export actorAlpha), or null for always.</summary>
        public readonly Func<double, Options, double> ActorAlpha;
        readonly Func<Options, Emitter, Effect> create;

        public string Title { get; set; }
        public string Desc { get; set; }
        public string[] Tags { get; set; }
        public string Bg { get; set; } = "dark";

        public Module(string name, string source, int w, int h, int ox, int oy, double duration, bool loop, int fps, Options defaults, Func<Options, Emitter, Effect> create,
            Act act = null, int actor = 0, Func<double, Options, double> actorAlpha = null)
        {
            Name = name;
            Source = source;
            W = w;
            H = h;
            OX = ox;
            OY = oy;
            Duration = duration;
            Loop = loop;
            Fps = fps;
            Defaults = defaults;
            this.create = create;
            Act = act;
            Actor = actor;
            ActorAlpha = actorAlpha;
        }

        public Effect Create(Options o, Emitter e) => create(o, e);

        /// <summary>Full-screen effects (transitions, weather, impact frames): 16:9 and at least 320 wide.</summary>
        public bool IsScreen => W >= 320 && Math.Abs((double)W / H - 16.0 / 9) < 0.05;

        /// <summary>The JS actorAlphaOf: how visible the stand-in character is at time t.</summary>
        public double ActorAlphaAt(double t, Options o) => ActorAlpha?.Invoke(t, o) ?? 1;
    }

    public sealed class Options
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public object this[string key]
        {
            get => values.TryGetValue(key, out object v) ? v : null;
            set => values[key] = value is int i ? (double)i : value;
        }

        public IEnumerable<KeyValuePair<string, object>> All => values;

        public bool Has(string key) => values.ContainsKey(key) && values[key] != null;
        public double Num(string key) => Convert.ToDouble(values[key]);
        public double Num(string key, double otherwise) => values.TryGetValue(key, out object v) && v != null ? Convert.ToDouble(v) : otherwise;
        public bool Bool(string key) => values.TryGetValue(key, out object v) && Truthy(v);
        public string Str(string key) => values.TryGetValue(key, out object v) ? v as string : null;
        public uint Color(string key) => values[key] is uint c ? c : Hex((string)values[key]);
        public string[] Ramp(string key) => values[key] is uint[] u ? Array.ConvertAll(u, ToHex) : (string[])values[key];
        public double Seed => Num("seed", 1);
        public Actor Actor => this["actor"] as Actor;

        /// <summary>JS truthiness of an option value.</summary>
        public static bool Truthy(object v) => v switch
        {
            null => false,
            bool b => b,
            double d => d != 0 && !double.IsNaN(d),
            string s => s.Length > 0,
            _ => true,
        };

        public Options Copy()
        {
            var o = new Options();
            foreach (var kv in values) o.values[kv.Key] = kv.Value;
            return o;
        }

        public static Options Resolve(Options defaults, Options caller)
        {
            var o = new Options();
            if (defaults != null)
                foreach (var kv in defaults.values)
                    o.values[kv.Key] = kv.Value;
            if (caller != null)
                foreach (var kv in caller.values)
                    o.values[kv.Key] = defaults != null && defaults.values.TryGetValue(kv.Key, out object def) && def is string[] d && kv.Value is string[] r && r.Length != d.Length
                        ? Array.ConvertAll(Fit(r, d.Length), ToHex)
                        : kv.Value;
            return o;
        }

        /// <summary>The JS resolveOptions: the module's defaults, the caller's on top, and the stock actor when the module wants one.</summary>
        public static Options Resolve(Module module, Options caller)
        {
            Options o = Resolve(module.Defaults, caller);
            if (module.Actor != 0 && !o.Bool("actor")) o["actor"] = Pixelfx.Actor.Stock(module.Actor == 2 ? 2 : 1);
            return o;
        }
    }

    public sealed class Fx
    {
        const double FadeSeconds = 0.3;

        readonly Effect impl;
        double stopAt = double.PositiveInfinity;

        public readonly Module Module;
        public readonly Options Options;
        public readonly Emitter Emitter = new Emitter();
        public double T { get; private set; }

        /// <summary>Where the effect's origin sits in frame space, mirrored, rotated: options x, y, flip, angle, and settable as it plays.</summary>
        public double X, Y, Angle;
        public bool Flip;
        public double LightK;

        public Fx(Module module, Options caller)
        {
            Module = module;
            Options = Options.Resolve(module, caller);
            X = Options.Num("x", 0);
            Y = Options.Num("y", 0);
            Flip = Options.Bool("flip");
            Angle = Options.Num("angle", 0);
            LightK = Options.Num("light", 1);
            impl = module.Create(Options, Emitter);
        }

        public void Update(double dt)
        {
            T += dt;
            int n = Math.Max(1, (int)Math.Ceiling(dt * 120 - 1e-9));
            for (int i = 0; i < n; i++) impl.Update(dt / n);
        }

        /// <summary>The effect's own clock as it draws: wrapped for a loop, held at the end of a one-shot.</summary>
        public double DrawT => Module.Loop ? T % Module.Duration : Math.Min(T, Module.Duration);

        public void Draw(Painter p, Layer layer = Layer.Both)
        {
            p.Place(X + Emitter.X, Y + Emitter.Y, Flip, Angle);
            p.Fade = double.IsPositiveInfinity(stopAt) ? 1 : Clamp(1 - (T - stopAt) / FadeSeconds);
            p.LightK = LightK;
            double tt = DrawT;
            if (layer != Layer.Front)
            {
                p.Layer = Layer.Back;
                impl.DrawBack(p, tt);
            }
            p.Shift(0, 0);
            if (layer != Layer.Back)
            {
                p.Layer = Layer.Front;
                impl.Draw(p, tt);
            }
        }

        public void Stop()
        {
            if (double.IsPositiveInfinity(stopAt)) stopAt = T;
        }

        public bool Done => T - stopAt >= FadeSeconds || (!Module.Loop && T >= Module.Duration);
    }

    public enum Layer
    {
        Both,
        Back,
        Front,
    }

    public sealed class Canvas
    {
        readonly Frame frame;
        readonly Painter painter;
        readonly byte[] rgba;
        readonly int margin;

        public readonly Fx Fx;
        public int W => frame.W;
        public int H => frame.H;
        public int PivotX => Fx.Module.OX + margin;
        public int PivotY => Fx.Module.OY + margin;

        public Canvas(Module module, Options caller, int margin = 0)
        {
            this.margin = margin;
            var o = new Options();
            if (caller != null)
                foreach (var kv in caller.All)
                    o[kv.Key] = kv.Value;
            o["x"] = (double)module.OX;
            o["y"] = (double)module.OY;
            Fx = new Fx(module, o);
            frame = new Frame(module.W + margin * 2, module.H + margin * 2, -margin, -margin);
            painter = new Painter(frame);
            rgba = new byte[frame.W * frame.H * 4];
        }

        public void MoveEmitter(int x, int y)
        {
            Fx.Emitter.X = x;
            Fx.Emitter.Y = y;
        }

        public byte[] Picture()
        {
            frame.Ox = Fx.Emitter.X - margin;
            frame.Oy = Fx.Emitter.Y - margin;
            frame.Clear();
            Fx.Draw(painter.Reset(frame));
            if (margin == 0 && !Fx.Module.IsScreen) frame.EdgeFade();
            frame.Composite(rgba);
            return rgba;
        }

        public static IEnumerable<byte[]> Frames(Module module, Options caller, int margin = 0)
        {
            var canvas = new Canvas(module, caller, margin);
            var shown = new Painter(new Frame(canvas.W, canvas.H, -margin, -margin));
            int n = Math.Max(1, RoundInt(module.Duration * module.Fps) + (module.Loop ? 0 : 1));
            for (int i = 0; i < n; i++)
            {
                if (i > 0) canvas.Fx.Update(1.0 / module.Fps);
                byte[] picture = canvas.Picture();
                canvas.Fx.Draw(shown.Reset(shown.Frame), Layer.Back);
                canvas.Fx.Draw(shown.Reset(shown.Frame), Layer.Front);
                yield return picture;
            }
        }
    }
}
