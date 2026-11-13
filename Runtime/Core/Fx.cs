using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public abstract class Effect
    {
        public virtual void Update(double dt) { }
        public virtual void DrawBack(Painter p) { }
        public abstract void Draw(Painter p);
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
        readonly Func<Options, Emitter, Effect> create;

        public Module(string name, string source, int w, int h, int ox, int oy, double duration, bool loop, int fps, Options defaults, Func<Options, Emitter, Effect> create)
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
        }

        public Effect Create(Options o, Emitter e) => create(o, e);
    }

    public sealed class Options
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public object this[string key]
        {
            get => values.TryGetValue(key, out object v) ? v : null;
            set => values[key] = value;
        }

        public IEnumerable<KeyValuePair<string, object>> All => values;

        public bool Has(string key) => values.ContainsKey(key);
        public double Num(string key) => Convert.ToDouble(values[key]);
        public double Num(string key, double otherwise) => values.TryGetValue(key, out object v) && v != null ? Convert.ToDouble(v) : otherwise;
        public bool Bool(string key) => values.TryGetValue(key, out object v) && v is bool b && b;
        public uint Color(string key) => values[key] is uint c ? c : Hex((string)values[key]);
        public string[] Ramp(string key) => (string[])values[key];
        public double Seed => Num("seed", 1);

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
    }

    public sealed class Fx
    {
        const double FadeSeconds = 0.3;

        readonly Effect impl;
        readonly double x, y, angle, lightK;
        readonly bool flip;
        double stopAt = double.PositiveInfinity;

        public readonly Module Module;
        public readonly Options Options;
        public readonly Emitter Emitter = new Emitter();
        public double T { get; private set; }

        public Fx(Module module, Options caller)
        {
            Module = module;
            Options = Options.Resolve(module.Defaults, caller);
            x = Options.Num("x", 0);
            y = Options.Num("y", 0);
            flip = Options.Bool("flip");
            angle = Options.Num("angle", 0);
            lightK = Options.Num("light", 1);
            impl = module.Create(Options, Emitter);
        }

        public void Update(double dt)
        {
            T += dt;
            int n = Math.Max(1, (int)Math.Ceiling(dt * 120 - 1e-9));
            for (int i = 0; i < n; i++) impl.Update(dt / n);
        }

        public void Draw(Painter p, Layer layer = Layer.Both)
        {
            p.Place(x + Emitter.X, y + Emitter.Y, flip, angle);
            p.Fade = double.IsPositiveInfinity(stopAt) ? 1 : Clamp(1 - (T - stopAt) / FadeSeconds);
            p.LightK = lightK;
            if (layer != Layer.Front) impl.DrawBack(p);
            p.Shift(0, 0);
            if (layer != Layer.Back) impl.Draw(p);
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
            if (margin == 0) frame.EdgeFade();
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
