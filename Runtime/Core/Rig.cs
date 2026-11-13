using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>
    /// The character who acts: a real skeleton in 3D, drawn down to pixels the way Dead Cells makes its sprites. Bones have fixed
    /// lengths and hang from each other, pelvis to spine to chest to shoulders, so the body can twist, bend and turn round, and depth
    /// decides what's in front. A pose only says where the hands and feet should be and how the body is carrying itself; the solver
    /// works out every joint, and the renderer shades it into a few flat tones with one clean ink line. A port of pixelfx's src/core/rig.js.
    ///
    /// Coordinates: x right, y up, z towards the viewer, in pixels, the feet at the origin. Poses are in screen terms (y negative is up).
    /// The character faces +x with his right side towards the viewer, so the right hand is the near one.
    /// A rendered pose also knows, for each of its pixels, which pixel of the standing character it came from (Src): that is what lets an
    /// effect drawn on the standing body follow the body wherever it goes.
    /// </summary>
    public static class Rig
    {
        /// <summary>The pixels of a pose, in local space with the feet at (0, 0).</summary>
        public sealed class Pixels
        {
            public int N;
            public short[] Xs, Ys, Src;
            public uint[] Color;
            public byte[] Ink;
            public int X0, Y0, X1, Y1;
            public double Z;
        }

        /// <summary>One part of the figure drawn alone (posedParts), with where its middle is.</summary>
        public sealed class Part
        {
            public string Name;
            public int Id, N;
            public short[] Xs, Ys;
            public uint[] Color;
            public double Cx, Cy, Z;
        }

        /// <summary>The skeleton of a pose: joints by name ([x, y, z], y down, feet at (0, 0)), bones between them, and the handles a pose moves.</summary>
        public sealed class Skeleton
        {
            public Dictionary<string, double[]> Joints;
            public string[][] Bones;
            public string[] Handles;
        }

        static readonly Dictionary<char, uint> PAL = Actor.DUMMY_PAL.ToDictionary(kv => kv.Key, kv => Hex(kv.Value));
        static readonly uint INK = PAL['k'], SEAM = PAL['J'], MARK = PAL['m'];
        // plates and joints: lit to dark
        static readonly uint[] SHELL = { PAL['A'], PAL['B'], PAL['C'], PAL['D'] }, CORE = { PAL['E'], PAL['F'], PAL['G'], PAL['G'] };
        const double DEG = Math.PI / 180;
        const int W0 = 24, H0 = 38;

        // ---- vectors and turns
        static double[] V(double x, double y, double z) => new[] { x, y, z };
        static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        static double[] Mul(double[] a, double s) => new[] { a[0] * s, a[1] * s, a[2] * s };
        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        static double Len(double[] a) => Px.Hypot(a[0], a[1], a[2]);

        static double[] Unit(double[] a)
        {
            double l = Len(a);
            return Mul(a, 1 / (l != 0 && !double.IsNaN(l) ? l : 1e-6)); // JS: 1 / (len(a) || 1e-6)
        }

        static double[] RotY(double t)
        {
            double c = Px.Cos(t), s = Px.Sin(t);
            return new[] { c, 0, s, 0, 1, 0, -s, 0, c };
        }

        static double[] RotZ(double t)
        {
            double c = Px.Cos(t), s = Px.Sin(t);
            return new[] { c, -s, 0, s, c, 0, 0, 0, 1 };
        }

        static double[] MM(double[] A, double[] B)
        {
            var o = new double[9];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    o[r * 3 + c] = A[r * 3] * B[c] + A[r * 3 + 1] * B[3 + c] + A[r * 3 + 2] * B[6 + c];
            return o;
        }

        static double[] MV(double[] M, double[] v) => new[] { M[0] * v[0] + M[1] * v[1] + M[2] * v[2], M[3] * v[0] + M[4] * v[1] + M[5] * v[2], M[6] * v[0] + M[7] * v[1] + M[8] * v[2] };
        static double[] TV(double[] M, double[] v) => new[] { M[0] * v[0] + M[3] * v[1] + M[6] * v[2], M[1] * v[0] + M[4] * v[1] + M[7] * v[2], M[2] * v[0] + M[5] * v[1] + M[8] * v[2] };
        static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));


        // ---- the body's measurements, in pixels
        const double Pelvis = 19.7, Spine1 = 2.8, Spine2 = 4.6, ShoulderUp = 2.5, ShoulderOut = 5.9, NeckUp = 4.0, HeadUp = 3.1,
            HipDown = 1.1, HipOut = 2.8, Thigh = 8.6, Shin = 8.3, Upper = 7.6, Fore = 7.4, Turn = 36 * DEG;

        /// <summary>Standing easy, in screen terms: hands and feet are where they should end up, y negative is up.</summary>
        public static readonly Pose STAND = new Pose { ["lean"] = 0, ["crouch"] = 0, ["yaw"] = 0, ["fx"] = -3, ["fy"] = -15, ["bx"] = 5, ["by"] = -15, ["ffx"] = -2, ["ffy"] = -4, ["fbx"] = 3, ["fby"] = -4 };

        // ---- solving a pose: from where the hands and feet should be, to every joint
        sealed class Ik
        {
            public double[] Joint, End, U, V;
        }

        static Ik Solve2(double[] a, double[] target, double L1, double L2, double[] pole)
        {
            double[] to = Sub(target, a);
            double d = Len(to);
            double reach = L1 + L2 - 0.02;
            if (d > reach)
            {
                to = Mul(to, reach / d);
                d = reach;
            }
            double[] u = Unit(to);
            double along = (L1 * L1 - L2 * L2 + d * d) / (2 * d), h = Math.Sqrt(Math.Max(0, L1 * L1 - along * along));
            double[] v = Sub(pole, Mul(u, Dot(pole, u)));
            v = Len(v) < 1e-4 ? Unit(Cross(u, V(0, 0, 1))) : Unit(v);
            return new Ik { Joint = Add(a, Add(Mul(u, along), Mul(v, h))), End = Add(a, to), U = u, V = v };
        }

        sealed class Bone
        {
            public double[] T, R;

            public Bone(double[] t, double[] r)
            {
                T = t;
                R = r;
            }
        }

        // a frame along a bone: its local x runs down the bone
        static Bone BoneFrame(double[] a, double[] b, double[] pole)
        {
            double[] x = Unit(Sub(b, a));
            double[] y = Sub(pole, Mul(x, Dot(pole, x)));
            y = Len(y) < 1e-4 ? Unit(Cross(x, V(0, 0, 1))) : Unit(y);
            double[] z = Cross(x, y);
            return new Bone(a, new[] { x[0], y[0], z[0], x[1], y[1], z[1], x[2], y[2], z[2] });
        }

        sealed class Solved
        {
            public Dictionary<string, Bone> B;
            public Weapon W, OW;
            public Pose Q;
            public double[] Axis, Side, SN, SF, HipN, HipF;
            public Ik ArmN, ArmF, LegN, LegF;
            public double Turn;
        }

        static Weapon WeaponOf(Pose q, string key)
        {
            string name = q.Str(key);
            return !string.IsNullOrEmpty(name) && WEAPONS.TryGetValue(name, out Weapon w) ? w : null;
        }

        static Solved Solve(Pose pose)
        {
            var q = new Pose(STAND, pose);
            double lean = q.N("lean") ?? double.NaN;
            Weapon W = WeaponOf(q, "weapon"), OW = WeaponOf(q, "offWeapon");
            double sa = q.N("sa") ?? 0, wl = q.N("wl") ?? 1, axisZ = Math.Sqrt(Math.Max(0, 1 - wl * wl)) * (q.N("wz") ?? 1);
            double[] axis = V(Px.Cos(sa) * wl, -Px.Sin(sa) * wl, axisZ), side = V(-Px.Sin(sa), -Px.Cos(sa), 0);
            bool two = q.Truthy("two") && W != null;
            double nearZ = q.N("fz") ?? (two ? 3 : 4.5);
            double qfx = q.N("fx") ?? double.NaN, qfy = q.N("fy") ?? double.NaN, qbx = q.N("bx") ?? double.NaN, qby = q.N("by") ?? double.NaN;
            // how the chest is turned follows the hands: a hand driving forward brings its shoulder round; one drawn back opens him up
            double fwd = (qfx - STAND.N("fx").Value) / 24, farFwd = two ? 0 : (qbx - STAND.N("bx").Value) / 22;
            double turn = Turn + (fwd >= 0 ? -44 * DEG * Math.Min(1, fwd) : 28 * DEG * Math.Min(1, -fwd * 1.8));
            turn += (farFwd > 0 ? 34 * DEG * Math.Min(1, farFwd) : 0) + (q.N("twist") ?? 0) * 4 * DEG;
            if (q.N("turn") is double qt) turn = qt;
            double hipTurn = Turn + (turn - Turn) * 0.45, tilt = Px.Atan2(lean, 13);
            double[] Rp = MM(RotZ(-tilt * 0.25), RotY(-hipTurn)), Rc = MM(RotZ(-tilt), RotY(-turn));
            // feet first: the pelvis sinks if that's what it takes for both legs to reach them
            double[] ankleN = V(q.N("ffx") ?? double.NaN, -(q.N("ffy") ?? double.NaN) - 1.8, q.N("fzz") ?? 2.4);
            double[] ankleF = V(q.N("fbx") ?? double.NaN, -(q.N("fby") ?? double.NaN) - 1.8, q.N("bzz") ?? -2.4);
            double[] pelvis = V(lean * 0.2, Pelvis - (q.N("crouch") ?? double.NaN), 0);
            double[] HipAt(double s) => Add(pelvis, MV(Rp, V(0, -HipDown, s * HipOut)));
            double maxLeg = (Thigh + Shin) * 0.995;
            foreach (var (s, a) in new[] { (1.0, ankleN), (-1.0, ankleF) })
            {
                double[] h = HipAt(s);
                double dx = h[0] - a[0], dz = h[2] - a[2], flat = dx * dx + dz * dz;
                if (flat < maxLeg * maxLeg)
                {
                    double top = a[1] + Math.Sqrt(maxLeg * maxLeg - flat);
                    if (h[1] > top) pelvis = V(pelvis[0], pelvis[1] - (h[1] - top), pelvis[2]);
                }
            }
            double[] waist = Add(pelvis, V(Px.Sin(tilt * 0.45) * Spine1, Px.Cos(tilt * 0.45) * Spine1, 0));
            double[] chest = Add(waist, V(Px.Sin(tilt) * Spine2, Px.Cos(tilt) * Spine2, 0));
            double[] neck = Add(chest, MV(Rc, V(0, NeckUp, 0)));
            // the head keeps looking where he's going: it takes only part of the lean and part of the turn
            double headTurn = Turn * 0.9 + (turn - Turn) * 0.35, nod = tilt * 0.35 + (q.N("hy") ?? 0) * 7 * DEG, roll = (q.N("hx") ?? 0) * 7 * DEG;
            double[] Rh = MM(RotZ(-nod - roll), RotY(-headTurn));
            double[] head = Add(neck, MV(Rh, V(0.15, HeadUp, 0)));
            // the arms: shoulders reach a little towards a far hand
            double[] Pick(double s, double[] target)
            {
                double[] bas = Add(chest, MV(Rc, V(0, ShoulderUp, s * ShoulderOut))), to = Sub(target, bas);
                double d = Len(to);
                return Add(bas, Mul(Unit(to), Math.Min(1.8, Math.Max(0, d - 11) * 0.35)));
            }
            double[] nearT = V(qfx, -qfy, nearZ);
            double[] farT = two
                ? V(qfx + axis[0] * W.Two, -qfy + axis[1] * W.Two, nearZ + axis[2] * W.Two)
                : V(qbx, -qby, q.N("bz") ?? (q.Truthy("armsFront") ? 5.5 : -4.5));
            double[] sN = Pick(1, nearT), sF = Pick(-1, farT);
            double[] Pole(double s, double[] T, double[] shoulder, double[] hold)
            {
                // elbows hang down and back; overhead they go out and forward
                double up = Clamp01((T[1] - shoulder[1] + 2) / 10);
                double[] bas = V(-0.55 + up * 1.1, -1 + up * 0.9, s * (0.45 + up * 0.5));
                if (hold == null) return bas;
                // holding something, the forearm wants to meet the handle side on: try round the circle, keep the best
                double[] u = Unit(Sub(T, shoulder));
                double[] best = bas;
                double score = double.PositiveInfinity;
                for (int i = 0; i < 16; i++)
                {
                    double a = ((double)i / 16) * Math.PI * 2;
                    double[] x = Unit(Cross(u, V(0.3, 1, 0.2))), y = Cross(u, x), v = Add(Mul(x, Px.Cos(a)), Mul(y, Px.Sin(a)));
                    Ik A = Solve2(shoulder, T, Upper, Fore, v);
                    double[] f = Unit(Sub(A.End, A.Joint));
                    double cost = Math.Abs(Dot(f, hold)) * 0.7 + (1 - Dot(Unit(bas), v)) * 0.55;
                    if (cost < score)
                    {
                        score = cost;
                        best = v;
                    }
                }
                return best;
            }
            double sb = q.N("sb") ?? Math.PI;
            double[] holdN = W != null ? (W.Across ? side : axis) : null;
            double[] holdF = two ? (W.Across ? side : axis) : OW != null ? V(Px.Cos(sb), -Px.Sin(sb), 0) : null;
            Ik armN = Solve2(sN, nearT, Upper, Fore, Pole(1, nearT, sN, holdN)), armF = Solve2(sF, farT, Upper, Fore, Pole(-1, farT, sF, holdF));
            // the legs: knees forward, a little out
            double[] KneePole(double s) => MV(Rp, V(1, 0, s * 0.35));
            double[] hipN = HipAt(1), hipF = HipAt(-1);
            Ik legN = Solve2(hipN, ankleN, Thigh, Shin, KneePole(1)), legF = Solve2(hipF, ankleF, Thigh, Shin, KneePole(-1));
            double[] FootDir(double s)
            {
                double t = hipTurn * 0.6 + s * 8 * DEG;
                return V(Px.Cos(t), 0, Px.Sin(t));
            }
            var B = new Dictionary<string, Bone>
            {
                ["pelvis"] = new Bone(pelvis, Rp),
                ["waist"] = new Bone(waist, MM(RotZ(-tilt * 0.6), RotY(-(turn + hipTurn) / 2))),
                ["chest"] = new Bone(chest, Rc),
                ["head"] = new Bone(head, Rh),
                ["neck"] = new Bone(neck, Rh),
                ["upperN"] = BoneFrame(sN, armN.Joint, armN.V),
                ["foreN"] = BoneFrame(armN.Joint, armN.End, armN.V),
                ["upperF"] = BoneFrame(sF, armF.Joint, armF.V),
                ["foreF"] = BoneFrame(armF.Joint, armF.End, armF.V),
                ["thighN"] = BoneFrame(hipN, legN.Joint, legN.V),
                ["shinN"] = BoneFrame(legN.Joint, legN.End, legN.V),
                ["thighF"] = BoneFrame(hipF, legF.Joint, legF.V),
                ["shinF"] = BoneFrame(legF.Joint, legF.End, legF.V),
            };
            double[] fdN = FootDir(1), fdF = FootDir(-1);
            B["footN"] = new Bone(legN.End, MM(RotY(-Px.Atan2(fdN[2], fdN[0])), RotZ(q.Truthy("toeF") ? -55 * DEG : 0)));
            B["footF"] = new Bone(legF.End, MM(RotY(-Px.Atan2(fdF[2], fdF[0])), RotZ(q.Truthy("toeB") ? -55 * DEG : 0)));
            return new Solved { B = B, W = W, OW = OW, Q = q, Axis = axis, Side = side, ArmN = armN, ArmF = armF, LegN = legN, LegF = legF, SN = sN, SF = sF, HipN = hipN, HipF = hipF, Turn = turn };
        }

        // ---- drawing: every part goes through one depth buffer
        const int BW = 300, BH = 240, OX = 150, OY = 180; // the scratch grid, big enough for a raised scythe; feet at (OX, OY)
        static readonly float[] Zb = new float[BW * BH], Tone = new float[BW * BH], LX = new float[BW * BH], LY = new float[BW * BH];
        static readonly sbyte[] PartOf = new sbyte[BW * BH], Mat = new sbyte[BW * BH], Fr = new sbyte[BW * BH];
        static readonly uint[] Col = new uint[BW * BH];
        static readonly List<int> touched = new List<int>();
        static readonly double[] LIGHT = Unit(V(-0.55, 0.72, 0.5));
        static bool clipBody, clipWeapon;
        static int curFrameIndex, onlyPart;
        static readonly string[] FRAME_IDS = { "pelvis", "waist", "chest", "head", "neck", "upperN", "foreN", "upperF", "foreF", "thighN", "shinN", "thighF", "shinF", "footN", "footF" };
        static readonly object Sync = new object();

        static void Plot(int i, double z, double tone, int part, int mat, double wx, double wy, uint color)
        {
            if (onlyPart != 0 && part != onlyPart) return;
            if ((uint)i >= BW * BH) return; // a JS typed array ignores writes past its end
            if (PartOf[i] == 0) touched.Add(i);
            else if (z <= Zb[i]) return;
            Zb[i] = (float)z;
            Tone[i] = (float)tone;
            PartOf[i] = (sbyte)part;
            Mat[i] = (sbyte)mat;
            Fr[i] = (sbyte)curFrameIndex;
            LX[i] = (float)wx;
            LY[i] = (float)wy;
            Col[i] = color;
        }

        static int PartAt(int j) => (uint)j < BW * BH ? PartOf[j] : -1; // -1: past the end (JS undefined)

        static void Sphere(double[] c, double r, int part, int mat)
        {
            double x0 = Math.Floor(c[0] - r), x1 = Math.Ceiling(c[0] + r), y0 = Math.Floor(c[1] - r), y1 = Math.Ceiling(c[1] + r);
            for (double wy = y0; wy <= y1; wy++)
                for (double wx = x0; wx <= x1; wx++)
                {
                    double dx = wx - c[0], dy = wy - c[1], d2 = dx * dx + dy * dy;
                    if (d2 > r * r || (clipBody && wy < 1.5)) continue;
                    double dz = Math.Sqrt(r * r - d2);
                    double[] n = V(dx / r, dy / r, dz / r);
                    int i = (int)((OY - wy) * BW + (OX + wx));
                    Plot(i, c[2] + dz, Dot(n, LIGHT), part, mat, wx, wy, 0);
                }
        }

        static void Sweep(double[] a, double[] b, double ra, double rb, int part, int mat, double bulge = 0)
        {
            int n = (int)Math.Max(2, Math.Ceiling(Len(Sub(b, a)) / 0.4));
            for (int k = 0; k <= n; k++)
            {
                double u = (double)k / n;
                Sphere(Add(a, Mul(Sub(b, a), u)), ra + (rb - ra) * u + bulge * Px.Sin(u * Math.PI), part, mat);
            }
        }

        // an ellipsoid with radii r along the axes of frame R, seen along z
        static void Ellipsoid(double[] c, double[] R, double[] r, int part, int mat)
        {
            double big = Math.Max(Math.Max(r[0], r[1]), r[2]);
            double x0 = Math.Floor(c[0] - big), x1 = Math.Ceiling(c[0] + big), y0 = Math.Floor(c[1] - big), y1 = Math.Ceiling(c[1] + big);
            double[] D = { 1 / (r[0] * r[0]), 1 / (r[1] * r[1]), 1 / (r[2] * r[2]) };
            // A = R diag(D) R^T
            var A = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    A[i * 3 + j] = R[i * 3] * D[0] * R[j * 3] + R[i * 3 + 1] * D[1] * R[j * 3 + 1] + R[i * 3 + 2] * D[2] * R[j * 3 + 2];
            for (double wy = y0; wy <= y1; wy++)
                for (double wx = x0; wx <= x1; wx++)
                {
                    if (clipBody && wy < 1.5) continue;
                    double dx = wx - c[0], dy = wy - c[1];
                    double qa = A[8], qb = 2 * (A[2] * dx + A[5] * dy), qc = A[0] * dx * dx + 2 * A[1] * dx * dy + A[4] * dy * dy - 1, disc = qb * qb - 4 * qa * qc;
                    if (disc < 0) continue;
                    double dz = (-qb + Math.Sqrt(disc)) / (2 * qa);
                    double[] n = Unit(V(A[0] * dx + A[1] * dy + A[2] * dz, A[3] * dx + A[4] * dy + A[5] * dz, A[6] * dx + A[7] * dy + A[8] * dz));
                    Plot((int)((OY - wy) * BW + (OX + wx)), c[2] + dz, Dot(n, LIGHT), part, mat, wx, wy, 0);
                }
        }

        // parts: each has its own number, so edges between them can be seen
        static readonly (string name, int id)[] PARTS =
        {
            ("pelvis", 1), ("waist", 2), ("chest", 3), ("neck", 4), ("head", 5), ("face", 6), ("upperN", 7), ("elbowN", 8), ("foreN", 9), ("handN", 10),
            ("upperF", 11), ("elbowF", 12), ("foreF", 13), ("handF", 14), ("thighN", 15), ("kneeN", 16), ("shinN", 17), ("footN", 18), ("thighF", 19),
            ("kneeF", 20), ("shinF", 21), ("footF", 22), ("weapon", 23), ("offWeapon", 24),
        };
        const int P_PELVIS = 1, P_WAIST = 2, P_CHEST = 3, P_NECK = 4, P_HEAD = 5, P_FACE = 6, P_UPPER_N = 7, P_ELBOW_N = 8, P_FORE_N = 9, P_HAND_N = 10,
            P_UPPER_F = 11, P_ELBOW_F = 12, P_FORE_F = 13, P_HAND_F = 14, P_THIGH_N = 15, P_KNEE_N = 16, P_SHIN_N = 17, P_FOOT_N = 18, P_THIGH_F = 19,
            P_KNEE_F = 20, P_SHIN_F = 21, P_FOOT_F = 22, P_WEAPON = 23, P_OFF_WEAPON = 24;

        static void UseFrame(string name) => curFrameIndex = Array.IndexOf(FRAME_IDS, name);

        static void DrawBody(Solved S)
        {
            Dictionary<string, Bone> B = S.B;
            UseFrame("pelvis");
            Ellipsoid(Add(B["pelvis"].T, MV(B["pelvis"].R, V(0, -0.2, 0))), B["pelvis"].R, V(2.9, 2.6, 4.2), P_PELVIS, 0);
            UseFrame("waist");
            Sweep(B["pelvis"].T, B["chest"].T, 3, 3.3, P_WAIST, 1);
            UseFrame("chest");
            Ellipsoid(Add(B["chest"].T, MV(B["chest"].R, V(0.2, 0.9, 0))), B["chest"].R, V(3.4, 4.2, 5.5), P_CHEST, 0);
            UseFrame("neck");
            Sweep(B["neck"].T, Add(B["neck"].T, MV(B["head"].R, V(0, 1.6, 0))), 1.25, 1.15, P_NECK, 1);
            UseFrame("head");
            Ellipsoid(B["head"].T, B["head"].R, V(3, 3.3, 2.8), P_HEAD, 0);
            Ellipsoid(Add(B["head"].T, MV(B["head"].R, V(0.85, -0.45, 0))), B["head"].R, V(2.4, 2.7, 2.35), P_FACE, 0);
            void Arm(Ik A, double[] sh, int up, int el, int fo, int ha, string nameU, string nameF)
            {
                UseFrame(nameU);
                Sphere(sh, 2.5, up, 0);
                Sweep(sh, A.Joint, 2.1, 1.6, up, 0);
                Sphere(A.Joint, 1.45, el, 1);
                UseFrame(nameF);
                Sweep(A.Joint, A.End, 1.6, 1.2, fo, 0, 0.15);
                Sphere(A.End, 1.9, ha, 0);
            }
            Arm(S.ArmN, S.SN, P_UPPER_N, P_ELBOW_N, P_FORE_N, P_HAND_N, "upperN", "foreN");
            Arm(S.ArmF, S.SF, P_UPPER_F, P_ELBOW_F, P_FORE_F, P_HAND_F, "upperF", "foreF");
            void Leg(Ik L, double[] hip, int th, int kn, int sh, int fo, string nameT, string nameS, string nameFt)
            {
                UseFrame(nameT);
                Sweep(hip, L.Joint, 3.2, 2.2, th, 0);
                Sphere(L.Joint, 1.9, kn, 1);
                UseFrame(nameS);
                Sweep(L.Joint, L.End, 2.15, 1.4, sh, 0, 0.4);
                UseFrame(nameFt);
                double[] R = B[nameFt].R, a = B[nameFt].T;
                Sweep(Add(a, MV(R, V(-1.5, -0.6, 0))), Add(a, MV(R, V(4.6, -1.1, 0))), 1.7, 1.25, fo, 0);
            }
            Leg(S.LegN, S.HipN, P_THIGH_N, P_KNEE_N, P_SHIN_N, P_FOOT_N, "thighN", "shinN", "footN");
            Leg(S.LegF, S.HipF, P_THIGH_F, P_KNEE_F, P_SHIN_F, P_FOOT_F, "thighF", "shinF", "footF");
        }

        // ---- what they hold: drawn along the handle from the fist, through the same depth buffer
        /// <summary>A pen along a weapon's handle: T(i, k, c) paints i px along it from the fist and k px across, lifted off it by lift.</summary>
        public sealed class Along
        {
            readonly double[] H, axis, side, n;
            readonly int part;

            internal Along(double[] h, double[] axis, double[] side, int part)
            {
                H = h;
                this.axis = axis;
                this.side = side;
                this.part = part;
                n = Cross(axis, side);
            }

            public void T(double i, double k, uint c, double lift = 0)
            {
                double x = H[0] + axis[0] * i + side[0] * k + n[0] * lift, y = H[1] + axis[1] * i + side[1] * k + n[1] * lift, z = H[2] + axis[2] * i + side[2] * k + n[2] * lift;
                double wx = Round(x), wy = Round(y);
                if (clipWeapon && wy < 0) return;
                double idx = (OY - wy) * BW + (OX + wx);
                if (idx < 0 || idx >= BW * BH) return;
                curFrameIndex = -1;
                Plot((int)idx, z + 0.05, 2, part, 2, wx, wy, c);
            }

            public double[] At(double i, double k) => V(H[0] + axis[0] * i + side[0] * k, H[1] + axis[1] * i + side[1] * k, H[2] + axis[2] * i + side[2] * k);
        }

        static readonly Dictionary<string, uint> STEEL = new Dictionary<string, string>
        {
            ["hi"] = "#ffffff", ["a"] = "#e8edf6", ["b"] = "#a4afc6", ["d"] = "#68748f", ["gold"] = "#ffd84a", ["goldD"] = "#b8862a", ["grip"] = "#7a3a2a", ["gripD"] = "#4a2018",
            ["wood"] = "#8a5a32", ["woodD"] = "#5a3820", ["dark"] = "#3a3448", ["darkD"] = "#262033", ["olive"] = "#58663f", ["oliveA"] = "#7d8c58", ["oliveD"] = "#3a4529",
        }.ToDictionary(kv => kv.Key, kv => Hex(kv.Value));

        static readonly uint S_HI = STEEL["hi"], S_A = STEEL["a"], S_B = STEEL["b"], S_D = STEEL["d"], S_GOLD = STEEL["gold"], S_GOLD_D = STEEL["goldD"], S_GRIP = STEEL["grip"], S_GRIP_D = STEEL["gripD"],
            S_WOOD = STEEL["wood"], S_WOOD_D = STEEL["woodD"], S_DARK = STEEL["dark"], S_DARK_D = STEEL["darkD"], S_OLIVE = STEEL["olive"], S_OLIVE_A = STEEL["oliveA"], S_OLIVE_D = STEEL["oliveD"];

        static void Blade(Along T, double from, double length, double w)
        {
            for (double i = from; i <= from + length + 0.01; i += 0.4)
            {
                double r = i - from, wd = r > length - 5 ? Math.Max(0, ((length - r) / 5) * w) : w;
                for (double k = -wd; k <= wd + 0.01; k += 0.4) T.T(i, k, k < -w * 0.4 ? S_HI : k > w * 0.4 ? S_D : S_A);
            }
        }

        static void Haft(Along T, double from, double to, uint a, uint d, double r = 0.8)
        {
            for (double i = from; i <= to; i += 0.4)
                for (double k = -r; k <= r + 0.01; k += 0.4)
                    T.T(i, k, k > 0.1 ? d : a);
        }

        /// <summary>
        /// A weapon a hand can hold. The fist is at i = 0; Two is where the other hand goes (NaN: it takes one hand); Across: the handle
        /// lies across the weapon's length. Draw(T, pose, the far hand).
        /// </summary>
        public sealed class Weapon
        {
            public readonly double Two;
            public readonly bool Across;
            internal readonly Action<Along, Pose, double[]> Draw;

            internal Weapon(double two, bool across, Action<Along, Pose, double[]> draw)
            {
                Two = two;
                Across = across;
                Draw = draw;
            }
        }

        static void BowLine(double[] A, double[] B, uint? c)
        {
            int n = (int)Math.Max(2, Math.Ceiling(Len(Sub(B, A)) / 0.4));
            uint Z = S_A;
            for (int k = 0; k <= n; k++)
            {
                double[] P = Add(A, Mul(Sub(B, A), (double)k / n));
                double wx = Round(P[0]), wy = Round(P[1]);
                double idx = (OY - wy) * BW + (OX + wx);
                curFrameIndex = -1;
                if (idx >= 0 && idx < BW * BH) Plot((int)idx, P[2] + 0.05, 2, P_WEAPON, 2, wx, wy, c ?? Z);
            }
        }

        public static readonly IReadOnlyDictionary<string, Weapon> WEAPONS = new Dictionary<string, Weapon>
        {
            ["sword"] = new Weapon(-4.4, false, (T, q, off) =>
            {
                double end = q.Truthy("two") ? -6.5 : -2.6;
                Haft(T, end, 2.2, S_GRIP, S_GRIP_D);
                for (double k = -1.4; k <= 1.4; k += 0.4) T.T(end - 0.4, k, S_GOLD);
                for (double k = -4.6; k <= 4.6; k += 0.4)
                {
                    T.T(2.6, k, Math.Abs(k) > 3.8 ? S_GOLD_D : S_GOLD);
                    T.T(3, k, S_GOLD_D);
                }
                Blade(T, 3.2, 25, 2.2);
            }),
            ["greatsword"] = new Weapon(-4.8, false, (T, q, off) =>
            {
                Haft(T, -7.2, 2.2, S_GRIP, S_GRIP_D);
                for (double k = -1.8; k <= 1.8; k += 0.4) T.T(-7.6, k, S_GOLD);
                for (double k = -6.6; k <= 6.6; k += 0.4)
                {
                    T.T(2.6, k, Math.Abs(k) > 5.6 ? S_GOLD_D : S_GOLD);
                    T.T(3, k, S_GOLD_D);
                }
                Blade(T, 3.2, 31, 3);
                for (double i = 4; i <= 28; i += 0.4) T.T(i, 0, S_B);
            }),
            ["dagger"] = new Weapon(double.NaN, false, (T, q, off) =>
            {
                Haft(T, -2.4, 1.8, S_GRIP, S_GRIP_D);
                for (double k = -2.6; k <= 2.6; k += 0.4) T.T(2.2, k, S_GOLD_D);
                Blade(T, 2.6, 10, 1.3);
            }),
            ["spear"] = new Weapon(-12, false, (T, q, off) =>
            {
                Haft(T, -18, 36, S_WOOD, S_WOOD_D, 0.5);
                Blade(T, 36, 11, 2);
                for (double k = -2.4; k <= 2.4; k += 0.4) T.T(36, k, S_GOLD_D);
            }),
            ["hammer"] = new Weapon(-7, false, (T, q, off) =>
            {
                foreach (double lift in new[] { -0.8, 0, 0.8 })
                    for (double i = -10; i <= 18; i += 0.4)
                        for (double k = -0.8; k <= 0.81; k += 0.4)
                            T.T(i, k, k > 0.1 ? S_WOOD_D : S_WOOD, lift);
                // the head is a block: its two faces, and the steel between them
                for (double i = 18; i <= 28; i += 0.4)
                    for (double k = -9; k <= 9; k += 0.4)
                    {
                        uint face = Math.Abs(k) > 7.4 ? (k < 0 ? S_A : S_D) : i < 20 ? S_HI : i > 26.4 ? S_D : Math.Abs(k) < 2 ? S_GOLD : S_B;
                        T.T(i, k, face, 4.5);
                        T.T(i, k, face, -4.5);
                        if (i < 18.4 || i > 27.6 || Math.Abs(k) > 8.6)
                            for (double l = -4; l <= 4; l += 0.4)
                                T.T(i, k, k < 0 ? S_A : i > 27.6 ? S_D : S_B, l);
                    }
            }),
            ["scythe"] = new Weapon(-11, false, (T, q, off) =>
            {
                Haft(T, -16, 31, S_DARK, S_DARK_D, 0.5);
                for (double j = 0; j <= 28; j += 0.4)
                {
                    double u = j / 28, w = 3.2 * (1 - u) + 0.3, b = 31.5 - u * u * 12;
                    for (double k = -w; k <= w; k += 0.4) T.T(b + k, j + 0.8, k > w * 0.3 ? S_HI : k < -w * 0.4 ? S_D : S_B);
                }
            }),
            ["claw"] = new Weapon(double.NaN, false, (T, q, off) =>
            {
                foreach (double q0 in new[] { -2.4, 0, 2.4 })
                    for (double i = 2; i <= 9.5; i += 0.4)
                    {
                        double u = (i - 2) / 7.5, k = q0 + u * u * 1.8;
                        T.T(i, k, u > 0.55 ? S_HI : S_A);
                        if (u < 0.6) T.T(i, k + 0.8, S_D);
                    }
            }),
            ["bow"] = new Weapon(double.NaN, true, (T, q, off) =>
            {
                for (double u = -1; u <= 1.001; u += 0.025)
                {
                    double k = u * 17, i = 6 * (1 - u * u) - 1;
                    T.T(i, k, Math.Abs(u) > 0.85 ? S_GOLD_D : S_WOOD);
                    T.T(i + 0.8, k, S_WOOD_D);
                }
                double[] tipA = T.At(-1, -17), tipB = T.At(-1, 17), mid = q.Truthy("pull") ? off : T.At(-1, 0);
                BowLine(tipA, mid, null);
                BowLine(tipB, mid, null);
                if (q.Truthy("nocked"))
                {
                    double[] nock = mid, tip = T.At(13, 0);
                    BowLine(nock, tip, S_WOOD);
                    for (double i = 9; i <= 13; i += 0.4)
                        for (double k = -(13 - i) * 0.45; k <= (13 - i) * 0.45; k += 0.4)
                            T.T(i, k, S_HI);
                }
            }),
            ["pistol"] = new Weapon(double.NaN, true, (T, q, off) =>
            {
                for (double i = -1; i <= 9.5; i += 0.4)
                    for (double k = -4.2; k <= -2.4; k += 0.4)
                        T.T(i, k, k < -3.6 ? S_A : k > -2.9 ? S_D : S_B);
                for (double i = -1.2; i <= 1.2; i += 0.4)
                    for (double k = -2.4; k <= 2.4; k += 0.4)
                        T.T(i, k, S_GRIP_D);
                T.T(9, -4.6, S_HI);
            }),
            ["launcher"] = new Weapon(-9, false, (T, q, off) =>
            {
                for (double i = -14; i <= 22; i += 0.4)
                    for (double k = -6; k <= -1; k += 0.4)
                        T.T(i, k, k < -4.8 ? S_OLIVE_A : k > -2.2 ? S_OLIVE_D : Math.Abs(i - 3) < 1.4 ? S_GOLD : S_OLIVE);
                for (double k = -6.8; k <= -0.2; k += 0.4)
                {
                    T.T(22.4, k, S_B);
                    T.T(-14.4, k, S_D);
                }
            }),
            ["handle"] = new Weapon(double.NaN, false, (T, q, off) =>
            {
                Haft(T, -2.6, 6, S_GRIP, S_GRIP_D);
                for (double k = -1.6; k <= 1.6; k += 0.4)
                {
                    T.T(-3, k, S_GOLD);
                    T.T(6.4, k, S_GOLD);
                }
            }),
        };

        // ---- flat tones, seams, and one clean ink line
        static Pixels Finish(Solved S, Dictionary<string, Bone> restFrames)
        {
            int Dim(int part) => part >= P_UPPER_F && part <= P_HAND_F ? 1 : part >= P_THIGH_F && part <= P_FOOT_F ? 1 : 0;
            int ToneOf(double v, int part)
            {
                int t = v > 0.8 ? 0 : v > 0.42 ? 1 : v > 0.08 ? 2 : 3;
                return Math.Min(3, t + Dim(part));
            }
            var tones = new Dictionary<int, int>();
            foreach (int i in touched) tones[i] = ToneOf(Tone[i], PartOf[i]);
            // a lone pixel of a tone, with no neighbour like it on the same part, takes its neighbours' tone
            int[] near4 = { -1, 1, -BW, BW };
            var votes = new int[4];
            foreach (int i in touched)
            {
                if (Mat[i] == 2) continue;
                int t = tones[i];
                Array.Clear(votes, 0, 4);
                bool same = false;
                foreach (int d in near4)
                {
                    int j = i + d;
                    if (PartAt(j) != PartOf[i] || !tones.ContainsKey(j)) continue;
                    int u = tones[j];
                    votes[u]++;
                    if (u == t) same = true;
                }
                if (!same && votes.Any(v => v > 0)) tones[i] = Array.IndexOf(votes, votes.Max());
            }
            foreach (int i in touched)
            {
                if (Mat[i] == 2) continue;
                Col[i] = (Mat[i] == 1 ? CORE : SHELL)[tones[i]];
            }
            // where two parts meet: a soft seam if they touch, ink if one stands in front of the other
            var marks = new Dictionary<int, uint>();
            foreach (int i in touched)
                foreach (int d in new[] { 1, BW })
                {
                    int j = i + d;
                    if ((uint)j >= BW * BH) continue;
                    if (PartOf[j] == 0 || PartOf[j] == PartOf[i]) continue;
                    double zi = Zb[i], zj = Zb[j];
                    int far = zi < zj ? i : j;
                    double dz = Math.Abs(zi - zj);
                    bool steel = Mat[i] == 2 || Mat[j] == 2;
                    if (Mat[far] == 2 && dz < 1) continue;
                    bool ink = steel || dz > 2.2;
                    if (ink) marks[far] = INK;
                    else if (!marks.ContainsKey(far)) marks[far] = SEAM;
                }
            foreach (var kv in marks) Col[kv.Key] = kv.Value;
            // the mark on his chest, when it's facing us
            Dictionary<string, Bone> B = S.B;
            double[] chestAt = Add(B["chest"].T, MV(B["chest"].R, V(3, 1.6, -1.8)));
            if (MV(B["chest"].R, V(1, 0, 0))[2] > 0.15)
            {
                int i = (int)((OY - Round(chestAt[1])) * BW + (OX + Round(chestAt[0])));
                if ((uint)i < BW * BH && PartOf[i] == P_CHEST) Col[i] = MARK;
            }
            // the outline, and everything into a small list of pixels
            var list = touched.ToArray();
            var ring = new Dictionary<int, int>();
            var ringOrder = new List<int>();
            foreach (int i in list)
                foreach (int d in near4)
                {
                    int j = i + d;
                    if (PartAt(j) == 0 && !ring.ContainsKey(j))
                    {
                        ring[j] = i;
                        ringOrder.Add(j);
                    }
                }
            int n = list.Length + ring.Count;
            var o = new Pixels { N = n, Xs = new short[n], Ys = new short[n], Color = new uint[n], Src = new short[n], Ink = new byte[n] };
            short SrcFor(int i)
            {
                int f = Fr[i];
                if (f < 0 || restFrames == null) return -1;
                string name = FRAME_IDS[f];
                Bone A = B[name], R0 = restFrames[name];
                double wx = (i % BW) - OX, wy = OY - (i / BW);
                double[] P = V(wx, wy, Zb[i]);
                double[] lp = TV(A.R, Sub(P, A.T)), rp = Add(R0.T, MV(R0.R, lp));
                double c = Round(rp[0]) + 12, r = 38 - Round(rp[1]);
                return c < 0 || r < 0 || c >= W0 || r >= H0 ? (short)-1 : (short)(r * W0 + c);
            }
            int k = 0, x0 = (int)1e9, y0 = (int)1e9, x1 = (int)-1e9, y1 = (int)-1e9;
            void Push(int i, uint c, short s, bool isInk)
            {
                int x = (i % BW) - OX, y = (i / BW) - OY;
                o.Xs[k] = (short)x;
                o.Ys[k] = (short)y;
                o.Color[k] = c;
                o.Src[k] = s;
                o.Ink[k] = (byte)(isInk ? 1 : 0);
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
                k++;
            }
            foreach (int i in list) Push(i, Col[i], SrcFor(i), Col[i] == INK);
            foreach (int j in ringOrder) Push(j, INK, SrcFor(ring[j]), true);
            double zs = 0;
            foreach (int i in list) zs += Zb[i];
            o.X0 = x0;
            o.Y0 = y0;
            o.X1 = x1;
            o.Y1 = y1;
            o.Z = list.Length > 0 ? zs / list.Length : 0;
            foreach (int i in list) PartOf[i] = 0;
            touched.Clear();
            return o;
        }

        static void TurnAll(Solved S, double a)
        {
            // the whole figure turned about the vertical through the feet
            double[] R = RotY(-a);
            double[] P(double[] v) => MV(R, v);
            foreach (Bone f in S.B.Values)
            {
                f.T = P(f.T);
                f.R = MM(R, f.R);
            }
            foreach (Ik A in new[] { S.ArmN, S.ArmF, S.LegN, S.LegF })
            {
                A.Joint = P(A.Joint);
                A.End = P(A.End);
            }
            S.SN = P(S.SN);
            S.SF = P(S.SF);
            S.HipN = P(S.HipN);
            S.HipF = P(S.HipF);
            S.Axis = P(S.Axis);
            S.Side = P(S.Side);
        }

        static double Whole(Pose q) => (q.N("yaw") ?? 0) + (q.N("face") == -1 ? Math.PI : 0);
        static bool Truthy(double v) => v != 0 && !double.IsNaN(v);

        static Pixels Render(Pose pose)
        {
            Solved S = Solve(pose);
            Pose q = S.Q;
            double whole = Whole(q);
            if (Truthy(whole)) TurnAll(S, whole);
            bool air = (q.N("y") ?? 0) < -0.5;
            clipBody = !air;
            clipWeapon = !air;
            DrawBody(S);
            clipBody = false;
            if (S.W != null) S.W.Draw(new Along(S.ArmN.End, S.Axis, S.Side, P_WEAPON), q, S.ArmF.End);
            if (S.OW != null)
            {
                double sb = q.N("sb") ?? Math.PI;
                double[] ax = V(Px.Cos(sb), -Px.Sin(sb), 0), sd = V(-Px.Sin(sb), -Px.Cos(sb), 0);
                if (Truthy(whole))
                {
                    double[] R = RotY(-whole);
                    ax = MV(R, ax);
                    sd = MV(R, sd);
                }
                S.OW.Draw(new Along(S.ArmF.End, ax, sd, P_OFF_WEAPON), q, S.ArmF.End);
            }
            clipWeapon = false;
            return Finish(S, REST_FRAMES);
        }

        static readonly Dictionary<string, Bone> REST_FRAMES = Solve(new Pose()).B;

        static readonly Dictionary<string, Pixels> CACHE = new Dictionary<string, Pixels>();
        static readonly HashSet<string> Twentieths = new HashSet<string> { "sa", "sb", "wl", "yaw", "turn" };

        static string KeyOf(Pose p)
        {
            var s = new System.Text.StringBuilder((p.N("y") ?? 0) < -0.5 ? "air," : "");
            foreach (string k in p.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                object v = p[k];
                if (v == null || k == "x" || k == "y" || k == "ghost") continue;
                s.Append(k);
                s.Append(v switch
                {
                    double d => ((long)Round(Twentieths.Contains(k) ? d * 20 : d)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    bool b => b ? "true" : "false",
                    _ => v.ToString(),
                });
                s.Append(',');
            }
            return s.ToString();
        }

        /// <summary>Forget every rendered pose (the JS cache only empties itself past 6000 poses; this lets a check start the way a fresh process does).</summary>
        public static void ClearCache()
        {
            lock (Sync) CACHE.Clear();
        }

        /// <summary>How far short of where the pose wants them the hands fall, in px: arms don't stretch.</summary>
        public static double Shortfall(Pose pose)
        {
            lock (Sync)
            {
                Solved S = Solve(pose);
                Pose q = S.Q;
                double near = Len(Sub(S.ArmN.End, V(q.N("fx") ?? double.NaN, -(q.N("fy") ?? double.NaN), S.ArmN.End[2])));
                double far = q.Truthy("two") ? 0 : Len(Sub(S.ArmF.End, V(q.N("bx") ?? double.NaN, -(q.N("by") ?? double.NaN), S.ArmF.End[2])));
                return Math.Max(near, far);
            }
        }

        /// <summary>The pixels of a pose, in local space with the feet at (0, 0).</summary>
        public static Pixels Posed(Pose pose)
        {
            lock (Sync)
            {
                string key = KeyOf(pose);
                if (!CACHE.TryGetValue(key, out Pixels r))
                {
                    if (CACHE.Count > 6000) CACHE.Clear();
                    CACHE[key] = r = Render(pose);
                }
                return r;
            }
        }

        /// <summary>
        /// The skeleton of a pose, in the same local space as Posed(): x right, y down, feet at (0, 0), z toward the viewer, whole figure
        /// turned by its yaw. The handles are what a pose moves (hands and feet); everything else is solved from them.
        /// </summary>
        public static Skeleton SkeletonOf(Pose pose)
        {
            lock (Sync)
            {
                Solved S = Solve(pose);
                Pose q = S.Q;
                double whole = Whole(q);
                if (Truthy(whole)) TurnAll(S, whole);
                Dictionary<string, Bone> B = S.B;
                double[] At(Bone f, double[] v) => Add(f.T, MV(f.R, v));
                var J = new Dictionary<string, double[]>
                {
                    ["pelvis"] = B["pelvis"].T, ["waist"] = B["waist"].T, ["chest"] = B["chest"].T, ["neck"] = B["neck"].T, ["head"] = B["head"].T, ["crown"] = At(B["head"], V(0, 3.3, 0)),
                    ["shoulderN"] = S.SN, ["elbowN"] = S.ArmN.Joint, ["handN"] = S.ArmN.End, ["shoulderF"] = S.SF, ["elbowF"] = S.ArmF.Joint, ["handF"] = S.ArmF.End,
                    ["hipN"] = S.HipN, ["kneeN"] = S.LegN.Joint, ["ankleN"] = S.LegN.End, ["toeN"] = At(B["footN"], V(4.6, -1.1, 0)),
                    ["hipF"] = S.HipF, ["kneeF"] = S.LegF.Joint, ["ankleF"] = S.LegF.End, ["toeF"] = At(B["footF"], V(4.6, -1.1, 0)),
                };
                return new Skeleton
                {
                    Joints = J.ToDictionary(kv => kv.Key, kv => new[] { kv.Value[0], -kv.Value[1], kv.Value[2] }),
                    Bones = new[]
                    {
                        new[] { "pelvis", "waist" }, new[] { "waist", "chest" }, new[] { "chest", "neck" }, new[] { "neck", "head" }, new[] { "head", "crown" },
                        new[] { "chest", "shoulderN" }, new[] { "shoulderN", "elbowN" }, new[] { "elbowN", "handN" }, new[] { "chest", "shoulderF" }, new[] { "shoulderF", "elbowF" },
                        new[] { "elbowF", "handF" }, new[] { "pelvis", "hipN" }, new[] { "hipN", "kneeN" }, new[] { "kneeN", "ankleN" }, new[] { "ankleN", "toeN" },
                        new[] { "pelvis", "hipF" }, new[] { "hipF", "kneeF" }, new[] { "kneeF", "ankleF" }, new[] { "ankleF", "toeF" },
                    },
                    Handles = new[] { "handN", "handF", "ankleN", "ankleF" },
                };
            }
        }

        /// <summary>The figure taken apart: each part drawn alone, with its own outline, and where its middle is. For exploded views.</summary>
        public static Part[] PosedParts(Pose pose)
        {
            lock (Sync)
            {
                var o = new List<Part>();
                foreach (var (name, part) in PARTS)
                {
                    onlyPart = part;
                    Pixels r = Render(pose);
                    onlyPart = 0;
                    if (r.N == 0) continue;
                    double cx = 0, cy = 0;
                    for (int i = 0; i < r.N; i++)
                    {
                        cx += r.Xs[i];
                        cy += r.Ys[i];
                    }
                    o.Add(new Part { Name = name, Id = part, N = r.N, Xs = r.Xs, Ys = r.Ys, Color = r.Color, Cx = cx / r.N, Cy = cy / r.N, Z = r.Z });
                }
                return o.ToArray();
            }
        }
    }
}
